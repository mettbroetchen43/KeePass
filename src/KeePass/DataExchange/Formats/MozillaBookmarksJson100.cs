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

using KeePass.Resources;

using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace KeePass.DataExchange.Formats
{
	// 1.00
	internal sealed class MozillaBookmarksJson100 : FileFormatProvider
	{
		public override bool SupportsImport { get { return true; } }
		public override bool SupportsExport { get { return false; } }

		public override string FormatName { get { return "Mozilla Bookmarks JSON"; } }
		public override string DefaultExtension { get { return "json"; } }
		public override string ApplicationGroup { get { return KPRes.Browser; } }

		public override void Import(PwDatabase pdStorage, Stream sInput,
			IStatusLogger slLogger)
		{
			string strJson = MemUtil.ReadString(sInput, StrUtil.Utf8);
			if(string.IsNullOrEmpty(strJson)) return;

			JsonObject jo = new JsonObject(new CharStream(strJson));

			Dictionary<string, List<string>> dTags =
				new Dictionary<string, List<string>>();
			List<PwEntry> lCreatedEntries = new List<PwEntry>();

			AddObject(pdStorage.RootGroup, jo, pdStorage, false, dTags,
				lCreatedEntries);

			// Tags support (old versions)
			foreach(PwEntry pe in lCreatedEntries)
			{
				string strUri = pe.Strings.ReadSafe(PwDefs.UrlField);
				if(strUri.Length == 0) continue;

				foreach(KeyValuePair<string, List<string>> kvp in dTags)
				{
					foreach(string strTagUri in kvp.Value)
					{
						if(strUri.Equals(strTagUri, StrUtil.CaseIgnoreCmp))
							pe.AddTag(kvp.Key);
					}
				}
			}
		}

		private static void AddObject(PwGroup pgStorage, JsonObject jo, PwDatabase pd,
			bool bCreateSubGroups, Dictionary<string, List<string>> dTags,
			List<PwEntry> lCreatedEntries)
		{
			string strRoot = jo.GetValue<string>("root");
			if(string.Equals(strRoot, "tagsFolder", StrUtil.CaseIgnoreCmp))
			{
				ImportTags(jo, dTags);
				return;
			}

			JsonObject[] v = jo.GetValueArray<JsonObject>("children");
			if(v != null)
			{
				PwGroup pgNew;
				if(bCreateSubGroups)
				{
					pgNew = new PwGroup(true, true);
					pgNew.Name = (jo.GetValue<string>("title") ?? string.Empty);

					pgStorage.AddGroup(pgNew, true);
				}
				else pgNew = pgStorage;

				foreach(JsonObject joSub in v)
				{
					if(joSub == null) { Debug.Assert(false); continue; }

					AddObject(pgNew, joSub, pd, true, dTags, lCreatedEntries);
				}

				return;
			}

			PwEntry pe = new PwEntry(true, true);

			// SetString(pe, "Index", jo, "index", pd);
			SetString(pe, PwDefs.TitleField, jo, "title", pd);
			// SetString(pe, "ID", jo, "id", pd);
			SetString(pe, PwDefs.UrlField, jo, "uri", pd);
			// SetString(pe, "CharSet", jo, "charset", pd);

			foreach(JsonObject joAnno in jo.GetValueArray<JsonObject>("annos", true))
			{
				if(joAnno == null) { Debug.Assert(false); continue; }

				string strName = joAnno.GetValue<string>("name");
				string strValue = joAnno.GetValue<string>("value");

				if((strName == "bookmarkProperties/description") &&
					!string.IsNullOrEmpty(strValue))
					ImportUtil.Add(pe, PwDefs.NotesField, strValue, pd);
			}

			// Tags support (new versions)
			string strTags = jo.GetValue<string>("tags");
			if(!string.IsNullOrEmpty(strTags))
				StrUtil.AddTags(pe.Tags, strTags.Split(','));

			string strKeyword = jo.GetValue<string>("keyword");
			if(!string.IsNullOrEmpty(strKeyword))
				ImportUtil.Add(pe, "Keyword", strKeyword, pd);

			if((pe.Strings.ReadSafe(PwDefs.TitleField).Length != 0) ||
				(pe.Strings.ReadSafe(PwDefs.UrlField).Length != 0))
			{
				pgStorage.AddEntry(pe, true);
				lCreatedEntries.Add(pe);
			}
		}

		private static void SetString(PwEntry pe, string strEntryKey,
			JsonObject jo, string strObjectKey, PwDatabase pd)
		{
			string str = jo.GetValue<string>(strObjectKey);
			if(string.IsNullOrEmpty(str)) return;

			ImportUtil.Add(pe, strEntryKey, str, pd);
		}

		// Tags support (old versions)
		private static void ImportTags(JsonObject joTagsRoot,
			Dictionary<string, List<string>> dTags)
		{
			JsonObject[] v = joTagsRoot.GetValueArray<JsonObject>("children");
			if(v == null) { Debug.Assert(false); return; }

			foreach(JsonObject joTag in v)
			{
				if(joTag == null) { Debug.Assert(false); continue; }

				string strName = joTag.GetValue<string>("title");
				if(string.IsNullOrEmpty(strName)) { Debug.Assert(false); continue; }

				List<string> lUris;
				dTags.TryGetValue(strName, out lUris);
				if(lUris == null)
				{
					lUris = new List<string>();
					dTags[strName] = lUris;
				}

				JsonObject[] vUris = joTag.GetValueArray<JsonObject>("children");
				if(vUris == null) { Debug.Assert(false); continue; }

				foreach(JsonObject joUri in vUris)
				{
					if(joUri == null) { Debug.Assert(false); continue; }

					string strUri = joUri.GetValue<string>("uri");
					if(!string.IsNullOrEmpty(strUri)) lUris.Add(strUri);
				}
			}
		}
	}
}
