/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2010 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Security.Cryptography;

using KeePass.App;
using KeePass.Ecas;
using KeePass.Native;
using KeePass.Util;
using KeePass.Util.Spr;

using KeePassLib;
using KeePassLib.Security;
using KeePassLib.Utility;

using KeeNativeLib = KeePassLib.Native;

namespace KeePass.Util
{
	public static class ClipboardUtil
	{
		private static string m_strFormat = null;
		private static byte[] m_pbDataHash32 = null;

		private const string ClipboardIgnoreFormatName = "Clipboard Viewer Ignore";

		public static bool Copy(string strToCopy, bool bIsEntryInfo,
			PwEntry peEntryInfo, PwDatabase pwReferenceSource)
		{
			Debug.Assert(strToCopy != null);
			if(strToCopy == null) throw new ArgumentNullException("strToCopy");

			return ClipboardUtil.Copy(new ProtectedString(false, strToCopy),
				bIsEntryInfo, peEntryInfo, pwReferenceSource);
		}

		public static bool Copy(ProtectedString psToCopy, bool bIsEntryInfo,
			PwEntry peEntryInfo, PwDatabase pwReferenceSource)
		{
			Debug.Assert(psToCopy != null);
			if(psToCopy == null) throw new ArgumentNullException("psToCopy");

			if(bIsEntryInfo && !AppPolicy.Try(AppPolicyId.CopyToClipboard))
				return false;

			string strData = SprEngine.Compile(psToCopy.ReadString(), false,
				peEntryInfo, pwReferenceSource, false, false);

			try
			{
				ClipboardUtil.Clear();

				DataObject doData = CreateProtectedDataObject(strData);
				Clipboard.SetDataObject(doData);

				m_pbDataHash32 = HashClipboard();
				m_strFormat = null;

				RaiseCopyEvent(bIsEntryInfo, strData);
			}
			catch(Exception) { Debug.Assert(false); return false; }

			if(peEntryInfo != null) peEntryInfo.Touch(false);

			// SprEngine.Compile might have modified the database
			Program.MainForm.UpdateUI(false, null, false, null, false, null, false);

			return true;
		}

		public static bool Copy(byte[] pbToCopy, string strFormat, bool bIsEntryInfo)
		{
			Debug.Assert(pbToCopy != null);
			if(pbToCopy == null) throw new ArgumentNullException("pbToCopy");

			if(bIsEntryInfo && !AppPolicy.Try(AppPolicyId.CopyToClipboard))
				return false;

			try
			{
				ClipboardUtil.Clear();

				DataObject doData = CreateProtectedDataObject(strFormat, pbToCopy);
				Clipboard.SetDataObject(doData);

				m_strFormat = strFormat;

				SHA256Managed sha256 = new SHA256Managed();
				m_pbDataHash32 = sha256.ComputeHash(pbToCopy);

				RaiseCopyEvent(bIsEntryInfo, string.Empty);
			}
			catch(Exception) { Debug.Assert(false); return false; }

			return true;
		}

		public static bool CopyAndMinimize(string strToCopy, bool bIsEntryInfo,
			Form formContext, PwEntry peEntryInfo, PwDatabase pwReferenceSource)
		{
			return ClipboardUtil.CopyAndMinimize(new ProtectedString(false, strToCopy),
				bIsEntryInfo, formContext, peEntryInfo, pwReferenceSource);
		}

		public static bool CopyAndMinimize(ProtectedString psToCopy, bool bIsEntryInfo,
			Form formContext, PwEntry peEntryInfo, PwDatabase pwReferenceSource)
		{
			if(ClipboardUtil.Copy(psToCopy, bIsEntryInfo, peEntryInfo, pwReferenceSource))
			{
				if(formContext != null)
				{
					if(Program.Config.MainWindow.DropToBackAfterClipboardCopy)
						NativeMethods.LoseFocus(formContext);

					if(Program.Config.MainWindow.MinimizeAfterClipboardCopy)
						formContext.WindowState = FormWindowState.Minimized;
				}

				return true;
			}

			return false;
		}

		private static void RaiseCopyEvent(bool bIsEntryInfo, string strDesc)
		{
			if(bIsEntryInfo == false) return;

			Program.TriggerSystem.RaiseEvent(EcasEventIDs.CopiedEntryInfo, strDesc);
		}

		/// <summary>
		/// Safely clear the clipboard. The clipboard clearing method of the
		/// .NET framework sets the clipboard to an empty <c>DataObject</c> when
		/// invoking the clearing method -- this might cause incompatibilities
		/// with other applications. Therefore, the <c>Clear</c> method of
		/// <c>ClipboardUtil</c> first tries to clear the clipboard using
		/// native Windows functions (which *really* clear the clipboard).
		/// </summary>
		public static void Clear()
		{
			bool bNativeSuccess = true;
			try
			{
				if(!NativeMethods.OpenClipboard(IntPtr.Zero))
				{
					Debug.Assert(false);
					throw new InvalidOperationException();
				}

				if(!NativeMethods.EmptyClipboard()) { Debug.Assert(false); }
				if(!NativeMethods.CloseClipboard()) { Debug.Assert(false); }
			}
			catch(Exception) { bNativeSuccess = false; }

			if(bNativeSuccess) return;

			try { Clipboard.Clear(); } // Fallback to .NET framework method
			catch(Exception) { Debug.Assert(false); }
		}

		public static void ClearIfOwner()
		{
			// If we didn't copy anything or cleared it already: do nothing
			if(m_pbDataHash32 == null) return;
			if(m_pbDataHash32.Length != 32) { Debug.Assert(false); return; }

			byte[] pbHash = HashClipboard(); // Hash current contents
			if(pbHash == null) return; // Unknown data (i.e. no KeePass data)
			if(pbHash.Length != 32) { Debug.Assert(false); return; }

			if(!MemUtil.ArraysEqual(m_pbDataHash32, pbHash)) return;
			
			m_pbDataHash32 = null;
			m_strFormat = null;

			ClipboardUtil.Clear();
		}

		private static byte[] HashClipboard()
		{
			try
			{
				if(Clipboard.ContainsText())
				{
					string strData = Clipboard.GetText();
					byte[] pbUtf8 = Encoding.UTF8.GetBytes(strData);

					SHA256Managed sha256 = new SHA256Managed();
					return sha256.ComputeHash(pbUtf8);
				}
				else if(m_strFormat != null)
				{
					if(Clipboard.ContainsData(m_strFormat))
					{
						byte[] pbData = (byte[])Clipboard.GetData(m_strFormat);

						SHA256Managed sha256 = new SHA256Managed();
						return sha256.ComputeHash(pbData);
					}
				}
			}
			catch(Exception) { Debug.Assert(false); }

			return null;
		}

		private static DataObject CreateProtectedDataObject(string strText)
		{
			DataObject d = new DataObject();
			AttachIgnoreFormat(d);

			Debug.Assert(strText != null); if(strText == null) return d;

			if(strText.Length > 0) d.SetText(strText);
			return d;
		}

		private static DataObject CreateProtectedDataObject(string strFormat,
			byte[] pbData)
		{
			DataObject d = new DataObject();
			AttachIgnoreFormat(d);

			Debug.Assert(strFormat != null); if(strFormat == null) return d;
			Debug.Assert(pbData != null); if(pbData == null) return d;

			if(pbData.Length > 0) d.SetData(strFormat, pbData);
			return d;
		}

		private static void AttachIgnoreFormat(DataObject doData)
		{
			Debug.Assert(doData != null); if(doData == null) return;

			if(!Program.Config.Security.UseClipboardViewerIgnoreFormat) return;
			if(KeeNativeLib.NativeLib.IsUnix()) return;

			try
			{
				doData.SetData(ClipboardIgnoreFormatName, false, PwDefs.ProductName);
			}
			catch(Exception) { Debug.Assert(false); }
		}
	}
}
