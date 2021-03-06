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
using System.Drawing;
using System.IO;

using KeePass.App;
using KeePass.Resources;

using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Serialization;

namespace KeePass.DataExchange.Formats
{
	internal sealed class KeePassKdb2x : FileFormatProvider
	{
		public override bool SupportsImport { get { return true; } }
		public override bool SupportsExport { get { return true; } }

		public override string FormatName { get { return "KeePass KDBX (2.x)"; } }
		public override string DefaultExtension { get { return AppDefs.FileExtension.FileExt; } }
		public override string ApplicationGroup { get { return PwDefs.ShortProductName; } }

		public override bool SupportsUuids { get { return true; } }
		public override bool RequiresKey { get { return true; } }

		public override Image SmallIcon
		{
			get { return KeePass.Properties.Resources.B16x16_KeePass; }
		}

		public override void Import(PwDatabase pwStorage, Stream sInput,
			IStatusLogger slLogger)
		{
			Kdb4File kdb4 = new Kdb4File(pwStorage);
			kdb4.Load(sInput, Kdb4Format.Default, slLogger);
		}

		public override bool Export(PwExportInfo pwExportInfo, Stream sOutput,
			IStatusLogger slLogger)
		{
			Kdb4File kdb4 = new Kdb4File(pwExportInfo.ContextDatabase);
			kdb4.Save(sOutput, pwExportInfo.DataGroup, Kdb4Format.Default, slLogger);
			return true;
		}
	}
}
