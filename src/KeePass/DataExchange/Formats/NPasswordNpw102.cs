﻿/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2025 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;

using KeePass.Resources;
using KeePass.Util;

using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace KeePass.DataExchange.Formats
{
	// 1.0.2.41-1.0.2.44+
	internal sealed class NPasswordNpw102 : FileFormatProvider
	{
		private const string ElemGroup = "folder";

		private const string ElemEntry = "record";
		private const string ElemEntryUser = "login";
		private const string ElemEntryPassword = "pass";
		private const string ElemEntryPassword2 = "additional";
		private const string ElemEntryUrl = "link";
		private const string ElemEntryNotes = "comments";
		private const string ElemEntryExpires = "expires";
		private const string ElemEntryExpiryTime = "expire_date";

		private const string ElemEntryUnsupp0 = "data";
		private const string ElemEntryUnsupp1 = "script";

		private const string ElemAutoType = "macro";
		private const string ElemAutoTypePlh = "item";

		private const string ElemTags = "keywords";

		private const string ElemUnsupp0 = "settings";

		private const string Password2Key = PwDefs.PasswordField + " 2";

		private static Dictionary<string, string> g_dAutoTypeConv = null;

		public override bool SupportsImport { get { return true; } }
		public override bool SupportsExport { get { return false; } }

		public override string FormatName { get { return "nPassword NPW"; } }
		public override string DefaultExtension { get { return "npw"; } }
		public override string ApplicationGroup { get { return KPRes.PasswordManagers; } }

		public override void Import(PwDatabase pdStorage, Stream sInput,
			IStatusLogger slLogger)
		{
			if(g_dAutoTypeConv == null)
				g_dAutoTypeConv = new Dictionary<string, string>
				{
					{ @"{login}", @"{USERNAME}" },
					{ @"{password}", @"{PASSWORD}" },
					{ @"{additional key}", @"{S:" + Password2Key + @"}" },
					{ @"{url}", @"{URL}" },
					{ @"{memo}", @"{NOTES}" },
					{ @"[tab]", @"{TAB}" },
					{ @"[enter]", @"{ENTER}" }
				};

			byte[] pbData = MemUtil.Read(sInput);

			// nPassword has options for encrypting/compressing exports,
			// which are unsupported; the file must start with "<?xml"
			if((pbData.Length < 6) || (pbData[0] != 0x3C) || (pbData[1] != 0x3F) ||
				(pbData[2] != 0x78) || (pbData[3] != 0x6D) || (pbData[4] != 0x6C))
				throw new FormatException(KPRes.NoEncNoCompress);

			string strData = Encoding.Default.GetString(pbData);
			strData = strData.Replace(@"&", @"&amp;");

			XmlDocument xmlDoc = XmlUtilEx.LoadXmlDocumentFromString(strData);

			XmlNode xmlRoot = xmlDoc.DocumentElement;

			foreach(XmlNode xmlChild in xmlRoot.ChildNodes)
			{
				if(xmlChild.Name == ElemGroup)
					ReadGroup(xmlChild, pdStorage.RootGroup, pdStorage);
				else if(xmlChild.Name == ElemEntry)
					ReadEntry(xmlChild, pdStorage.RootGroup, pdStorage);
				else if(xmlChild.Name == ElemUnsupp0) { }
				else { Debug.Assert(false); }
			}
		}

		private static void ReadGroup(XmlNode xmlNode, PwGroup pgParent, PwDatabase pd)
		{
			PwGroup pg = new PwGroup(true, true);
			pgParent.AddGroup(pg, true);

			foreach(XmlNode xmlChild in xmlNode)
			{
				if(xmlChild.NodeType == XmlNodeType.Text)
				{
					string strValue = (xmlChild.Value ?? string.Empty).Trim();
					if(strValue.Length > 0)
					{
						if(pg.Name.Length > 0) pg.Name += " ";
						pg.Name += strValue;
					}
				}
				else if(xmlChild.Name == ElemGroup)
					ReadGroup(xmlChild, pg, pd);
				else if(xmlChild.Name == ElemEntry)
					ReadEntry(xmlChild, pg, pd);
				else if(xmlChild.Name == ElemTags)
					AddTags(pg.Tags, XmlUtil.SafeInnerText(xmlChild));
				else { Debug.Assert(false); }
			}
		}

		private static void ReadEntry(XmlNode xmlNode, PwGroup pgParent, PwDatabase pd)
		{
			PwEntry pe = new PwEntry(true, true);
			pgParent.AddEntry(pe, true);

			DateTime? odtExpiry = null;

			foreach(XmlNode xmlChild in xmlNode)
			{
				string strValue = XmlUtil.SafeInnerText(xmlChild);

				if(xmlChild.NodeType == XmlNodeType.Text)
					ImportUtil.AppendToField(pe, PwDefs.TitleField, (xmlChild.Value ??
						string.Empty).Trim(), pd, " ", false);
				else if(xmlChild.Name == ElemEntryUser)
					ImportUtil.Add(pe, PwDefs.UserNameField, strValue, pd);
				else if(xmlChild.Name == ElemEntryPassword)
					ImportUtil.Add(pe, PwDefs.PasswordField, strValue, pd);
				else if(xmlChild.Name == ElemEntryPassword2)
				{
					if(strValue.Length > 0) // Prevent empty item
						pe.Strings.Set(Password2Key, new ProtectedString(
							pd.MemoryProtection.ProtectPassword, strValue));
				}
				else if(xmlChild.Name == ElemEntryUrl)
					ImportUtil.Add(pe, PwDefs.UrlField, strValue, pd);
				else if(xmlChild.Name == ElemEntryNotes)
					ImportUtil.Add(pe, PwDefs.NotesField, strValue, pd);
				else if(xmlChild.Name == ElemTags)
					AddTags(pe.Tags, strValue);
				else if(xmlChild.Name == ElemEntryExpires)
					pe.Expires = StrUtil.StringToBool(strValue);
				else if(xmlChild.Name == ElemEntryExpiryTime)
				{
					DateTime dt;
					if(TimeUtil.FromDisplayStringEx(strValue, out dt))
						odtExpiry = TimeUtil.ToUtc(dt, false);
					else { Debug.Assert(false); }
				}
				else if(xmlChild.Name == ElemAutoType)
					ReadAutoType(xmlChild, pe);
				else if(xmlChild.Name == ElemEntryUnsupp0) { }
				else if(xmlChild.Name == ElemEntryUnsupp1) { }
				else { Debug.Assert(false); }
			}

			if(odtExpiry.HasValue) pe.ExpiryTime = odtExpiry.Value;
			else pe.Expires = false;
		}

		private static void ReadAutoType(XmlNode xmlNode, PwEntry pe)
		{
			string strSeq = string.Empty;

			foreach(XmlNode xmlChild in xmlNode)
			{
				if(xmlChild.Name == ElemAutoTypePlh)
				{
					string strValue = XmlUtil.SafeInnerText(xmlChild);

					string strConv = null;
					foreach(KeyValuePair<string, string> kvp in g_dAutoTypeConv)
					{
						if(kvp.Key.Equals(strValue, StrUtil.CaseIgnoreCmp))
						{
							strConv = kvp.Value;
							break;
						}
					}

					if(strConv != null) strSeq += strConv;
					else { Debug.Assert(false); strSeq += strValue; }
				}
				else { Debug.Assert(false); }
			}

			pe.AutoType.DefaultSequence = strSeq;
		}

		private static void AddTags(List<string> lTags, string strNewTags)
		{
			if(string.IsNullOrEmpty(strNewTags)) return;

			StrUtil.AddTags(lTags, StrUtil.StringToTags(
				strNewTags.Replace(' ', ';')));
		}
	}
}
