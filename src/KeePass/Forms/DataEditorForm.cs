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
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Text;
using System.Windows.Forms;

using KeePass.App;
using KeePass.App.Configuration;
using KeePass.Native;
using KeePass.Resources;
using KeePass.UI;
using KeePass.Util;

using KeePassLib;
using KeePassLib.Utility;

using NativeLib = KeePassLib.Native.NativeLib;

namespace KeePass.Forms
{
	public partial class DataEditorForm : Form
	{
		private string m_strDataDesc = string.Empty;
		private byte[] m_pbData = null;
		private byte[] m_pbEditedData = null;

		private bool m_bModified = false;
		private bool m_bURtfWithHighChar = false;

		private uint m_uBlockEvents = 0;
		private readonly Stack<KeyValuePair<int, int>> m_stSelections =
			new Stack<KeyValuePair<int, int>>();
		private BinaryDataClass m_bdc = BinaryDataClass.Unknown;
		private bool m_bNewLinesWin = true;

		private string m_strInitialFormRect = string.Empty;
		private readonly RichTextBoxContextMenu m_ctxText = new RichTextBoxContextMenu();

		/// <summary>
		/// Get the edited, new data. This property is non-<c>null</c> only
		/// if the user has really edited the data (i.e. if the user makes no
		/// changes, <c>null</c> is returned).
		/// </summary>
		public byte[] EditedBinaryData
		{
			get { return m_pbEditedData; }
		}

		public MenuStrip MainMenuEx { get { return m_menuMain; } }

		public static bool SupportsDataType(BinaryDataClass bdc)
		{
			return ((bdc == BinaryDataClass.Text) || (bdc == BinaryDataClass.RichText));
		}

		public void InitEx(string strDataDesc, byte[] pbData)
		{
			if(strDataDesc != null) m_strDataDesc = strDataDesc;

			m_pbData = pbData;
		}

		public DataEditorForm()
		{
			InitializeComponent();

			GlobalWindowManager.InitializeForm(this);
			Program.Translation.ApplyTo("KeePass.Forms.DataEditorForm.m_menuMain", m_menuMain.Items);
		}

		private void OnFormLoad(object sender, EventArgs e)
		{
			Debug.Assert(m_pbData != null);
			if(m_pbData == null) throw new InvalidOperationException();

			GlobalWindowManager.AddWindow(this);

			this.Icon = AppIcons.Default;
			this.DoubleBuffered = true;

			m_strInitialFormRect = UIUtil.SetWindowScreenRectEx(this,
				Program.Config.UI.DataEditorRect);

			m_bdc = BinaryDataClassifier.Classify(m_strDataDesc, m_pbData);
			uint uStartOffset;
			StrEncodingInfo seiGuess = BinaryDataClassifier.GetStringEncoding(
				m_pbData, out uStartOffset);
			string strData;
			try
			{
				strData = (seiGuess.Encoding.GetString(m_pbData, (int)uStartOffset,
					m_pbData.Length - (int)uStartOffset) ?? string.Empty);
				strData = StrUtil.ReplaceNulls(strData);
			}
			catch(Exception) { Debug.Assert(false); strData = string.Empty; }

			++m_uBlockEvents;

			UIUtil.AssignShortcut(m_menuFileSave, Keys.Control | Keys.S);
			UIUtil.AssignShortcut(m_menuFileExit, Keys.Escape, null, true);
			UIUtil.AssignShortcut(m_menuEditUndo, Keys.Control | Keys.Z, null, true);
			UIUtil.AssignShortcut(m_menuEditRedo, Keys.Control | Keys.Y, null, true);
			UIUtil.AssignShortcut(m_menuEditCut, Keys.Control | Keys.X, null, true);
			UIUtil.AssignShortcut(m_menuEditCopy, Keys.Control | Keys.C, null, true);
			UIUtil.AssignShortcut(m_menuEditPaste, Keys.Control | Keys.V, null, true);
			UIUtil.AssignShortcut(m_menuEditDelete, Keys.Delete, null, true);
			UIUtil.AssignShortcut(m_menuEditSelectAll, Keys.Control | Keys.A, null, true);
			UIUtil.AssignShortcut(m_menuEditFind, Keys.Control | Keys.F);

			UIUtil.ConfigureTbButton(m_tbFileSave, KPRes.Save, null, m_menuFileSave);
			UIUtil.ConfigureTbButton(m_tbEditCut, KPRes.Cut, null, m_menuEditCut);
			UIUtil.ConfigureTbButton(m_tbEditCopy, KPRes.Copy, null, m_menuEditCopy);
			UIUtil.ConfigureTbButton(m_tbEditPaste, KPRes.Paste, null, m_menuEditPaste);
			UIUtil.ConfigureTbButton(m_tbEditUndo, KPRes.Undo, null, m_menuEditUndo);
			UIUtil.ConfigureTbButton(m_tbEditRedo, KPRes.Redo, null, m_menuEditRedo);
			UIUtil.ConfigureTbButton(m_tbFind, null, KPRes.Find, m_menuEditFind);

			// Formatting keyboard shortcuts are implemented by CustomRichTextBoxEx
			UIUtil.ConfigureTbButton(m_tbFormatBold, KPRes.Bold, KPRes.Bold +
				" (" + UIUtil.GetKeysName(Keys.Control | Keys.B) + ")", null);
			UIUtil.ConfigureTbButton(m_tbFormatItalic, KPRes.Italic, KPRes.Italic +
				" (" + UIUtil.GetKeysName(Keys.Control | Keys.I) + ")", null);
			UIUtil.ConfigureTbButton(m_tbFormatUnderline, KPRes.Underline, KPRes.Underline +
				" (" + UIUtil.GetKeysName(Keys.Control | Keys.U) + ")", null);
			UIUtil.ConfigureTbButton(m_tbFormatStrikeout, KPRes.Strikeout, null);
			UIUtil.ConfigureTbButton(m_tbColorForeground, KPRes.TextColor, null);
			UIUtil.ConfigureTbButton(m_tbColorBackground, KPRes.BackgroundColor, null);
			UIUtil.ConfigureTbButton(m_tbAlignLeft, KPRes.AlignLeft, KPRes.AlignLeft +
				" (" + UIUtil.GetKeysName(Keys.Control | Keys.L) + ")", null);
			UIUtil.ConfigureTbButton(m_tbAlignCenter, KPRes.AlignCenter, KPRes.AlignCenter +
				" (" + UIUtil.GetKeysName(Keys.Control | Keys.E) + ")", null);
			UIUtil.ConfigureTbButton(m_tbAlignRight, KPRes.AlignRight, KPRes.AlignRight +
				" (" + UIUtil.GetKeysName(Keys.Control | Keys.R) + ")", null);

			string strSearchTr = ((WinUtil.IsAtLeastWindowsVista ?
				string.Empty : " ") + KPRes.Search);
			UIUtil.SetCueBanner(m_tbFind, strSearchTr);

			UIUtil.SetToolTip(m_tbFontCombo, KPRes.Font, true);
			UIUtil.SetToolTip(m_tbFontSizeCombo, KPRes.Size, true);

			UIUtil.EnableAutoCompletion(m_tbFontCombo, true);
			UIUtil.EnableAutoCompletion(m_tbFontSizeCombo, true);

			m_rtbText.WordWrap = Program.Config.UI.DataEditorWordWrap;
			m_ctxText.Attach(m_rtbText, this);
			m_tssStatusMain.Text = KPRes.Ready;

			InitFormattingToolBar();

			bool bSimpleText = true, bDefaultFont = true;
			if(m_bdc == BinaryDataClass.RichText)
			{
				try
				{
					if(strData.Length > 0)
					{
						m_rtbText.Rtf = StrUtil.RtfFix(strData);
						bDefaultFont = false;
					}
					else m_rtbText.Text = string.Empty;

					bSimpleText = false;
				}
				catch(Exception) { } // Show as simple text
			}

			if(bSimpleText)
			{
				m_rtbText.Text = strData;
				m_rtbText.SimpleTextOnly = true;

				// CR is upgraded to CR+LF
				m_bNewLinesWin = (StrUtil.GetNewLineSeq(strData) != "\n");
			}
			else m_menuViewFont.Text = KPRes.FontDefault + "...";

			if(bDefaultFont && Program.Config.UI.DataEditorFont.OverrideUIDefault)
			{
				m_rtbText.SelectAll();
				m_rtbText.SelectionFont = Program.Config.UI.DataEditorFont.ToFont();
			}

			m_rtbText.Select(0, 0);
			--m_uBlockEvents;
			UpdateUIState(false, true);
		}

		private void InitFormattingToolBar()
		{
			if(m_bdc != BinaryDataClass.RichText)
			{
				m_toolFormat.Visible = false;
				return;
			}

			using(InstalledFontCollection c = new InstalledFontCollection())
			{
				foreach(FontFamily ff in c.Families)
					m_tbFontCombo.Items.Add(ff.Name);
			}

			int[] vSizes = new int[] { 8, 9, 10, 11, 12, 14, 16, 18, 20,
				22, 24, 26, 28, 36, 48, 72 };
			foreach(int nSize in vSizes)
				m_tbFontSizeCombo.Items.Add(nSize.ToString());
		}

		private void UpdateUIState(bool bSetModified, bool bFocusText)
		{
			++m_uBlockEvents;
			if(bSetModified) m_bModified = true;

			this.Text = (((m_strDataDesc.Length > 0) ? (m_strDataDesc +
				(m_bModified ? "*" : string.Empty) + " - ") : string.Empty) +
				KPRes.DataEditorKP);

			// m_menuViewFont.Enabled = (m_bdc == BinaryDataClass.Text);
			UIUtil.SetChecked(m_menuViewWordWrap, m_rtbText.WordWrap);

			m_tbFileSave.Image = (m_bModified ? Properties.Resources.B16x16_FileSave :
				Properties.Resources.B16x16_FileSave_Disabled);

			UIUtil.SetEnabledFast(m_rtbText.CanUndo, m_menuEditUndo, m_tbEditUndo);
			UIUtil.SetEnabledFast(m_rtbText.CanRedo, m_menuEditRedo, m_tbEditRedo);

			bool bSel = (m_rtbText.SelectionLength != 0);
			UIUtil.SetEnabledFast(bSel, m_menuEditCut, m_tbEditCut,
				m_menuEditCopy, m_tbEditCopy, m_menuEditDelete);

			Font fSel = m_rtbText.SelectionFont;
			if(fSel != null)
			{
				m_tbFormatBold.Checked = fSel.Bold;
				m_tbFormatItalic.Checked = fSel.Italic;
				m_tbFormatUnderline.Checked = fSel.Underline;
				m_tbFormatStrikeout.Checked = fSel.Strikeout;

				string strFontName = fSel.Name;
				if(m_tbFontCombo.Items.IndexOf(strFontName) >= 0)
					m_tbFontCombo.SelectedItem = strFontName;
				else m_tbFontCombo.Text = strFontName;

				string strFontSize = fSel.SizeInPoints.ToString();
				if(m_tbFontSizeCombo.Items.IndexOf(strFontSize) >= 0)
					m_tbFontSizeCombo.SelectedItem = strFontSize;
				else m_tbFontSizeCombo.Text = strFontSize;
			}

			HorizontalAlignment ha = m_rtbText.SelectionAlignment;
			m_tbAlignLeft.Checked = (ha == HorizontalAlignment.Left);
			m_tbAlignCenter.Checked = (ha == HorizontalAlignment.Center);
			m_tbAlignRight.Checked = (ha == HorizontalAlignment.Right);

			--m_uBlockEvents;
			if(bFocusText) UIUtil.SetFocus(m_rtbText, this);
		}

		private void UISelectAllText(bool bSelect)
		{
			if(bSelect)
			{
				m_stSelections.Push(new KeyValuePair<int, int>(
					m_rtbText.SelectionStart, m_rtbText.SelectionLength));
				m_rtbText.SelectAll();
			}
			else
			{
				KeyValuePair<int, int> kvp = m_stSelections.Pop();
				m_rtbText.Select(kvp.Key, kvp.Value);
			}
		}

		private void OnFileSave(object sender, EventArgs e)
		{
			if(m_bdc == BinaryDataClass.RichText)
			{
				string strRtf = m_rtbText.Rtf;

				if(StrUtil.RtfIsURtf(strRtf))
					m_bURtfWithHighChar = StrUtil.ContainsHighChar(m_rtbText.Text);
				else m_bURtfWithHighChar = false;

				m_pbEditedData = StrUtil.Utf8.GetBytes(StrUtil.RtfFix(strRtf));
			}
			else
			{
				string strData = m_rtbText.Text;
				strData = StrUtil.NormalizeNewLines(strData, m_bNewLinesWin);
				m_pbEditedData = StrUtil.Utf8.GetBytes(strData);
			}

			m_bModified = false;
			UpdateUIState(false, false);
		}

		private void OnFormClosing(object sender, FormClosingEventArgs e)
		{
			if(m_bModified)
			{
				DialogResult dr = MessageService.Ask(KPRes.SaveBeforeCloseQuestion,
					PwDefs.ShortProductName, MessageBoxButtons.YesNoCancel);

				if(dr == DialogResult.Yes)
					OnFileSave(sender, EventArgs.Empty);
				else if(dr == DialogResult.No) { }
				else
				{
					e.Cancel = true;
					return;
				}
			}

			if(m_bURtfWithHighChar && (m_pbEditedData != null) &&
				!MemUtil.ArraysEqual(m_pbEditedData, m_pbData))
			{
				string strUrl = AppHelp.GetOnlineUrl(AppDefs.HelpTopics.KbFaq,
					AppDefs.HelpTopics.KbFaqURtf);
				string strLink = VistaTaskDialog.CreateLink(strUrl, strUrl);
				string strMsg = KPRes.URtfProblem + MessageService.NewParagraph +
					KPRes.URtfCheck + MessageService.NewParagraph +
					KPRes.URtfSuggestion + MessageService.NewParagraph +
					KPRes.MoreInfo + ":" + MessageService.NewLine;

				VistaTaskDialog dlg = new VistaTaskDialog();
				dlg.AddButton((int)DialogResult.Cancel, KPRes.Ok, null);
				dlg.CommandLinks = false;
				dlg.Content = strMsg + strLink;
				dlg.DefaultButtonID = (int)DialogResult.Cancel;
				dlg.EnableHyperlinks = true;
				dlg.SetIcon(VtdIcon.Warning);
				dlg.WindowTitle = PwDefs.ShortProductName;

				if(!dlg.ShowDialog())
					MessageService.ShowWarning(strMsg + strUrl);
			}

			Debug.Assert(m_uBlockEvents == 0);

			string strRect = UIUtil.GetWindowScreenRect(this);
			if(strRect != m_strInitialFormRect) // Don't overwrite ""
				Program.Config.UI.DataEditorRect = strRect;
		}

		private void OnFormClosed(object sender, FormClosedEventArgs e)
		{
			m_ctxText.Detach();
			GlobalWindowManager.RemoveWindow(this);
		}

		private void ToggleSelectionFormat(FontStyle fs)
		{
			if((m_uBlockEvents > 0) || (m_bdc != BinaryDataClass.RichText)) return;

			UIUtil.RtfToggleSelectionFormat(m_rtbText, fs);
			UpdateUIState(true, true);
		}

		private void OnFormatBoldClicked(object sender, EventArgs e)
		{
			ToggleSelectionFormat(FontStyle.Bold);
		}

		private void OnFormatItalicClicked(object sender, EventArgs e)
		{
			ToggleSelectionFormat(FontStyle.Italic);
		}

		private void OnFormatUnderlineClicked(object sender, EventArgs e)
		{
			ToggleSelectionFormat(FontStyle.Underline);
		}

		private void OnFormatStrikeoutClicked(object sender, EventArgs e)
		{
			ToggleSelectionFormat(FontStyle.Strikeout);
		}

		private void OnTextSelectionChanged(object sender, EventArgs e)
		{
			if(m_uBlockEvents > 0) return;

			UpdateUIState(false, false);
		}

		private void OnColorForegroundClicked(object sender, EventArgs e)
		{
			if((m_uBlockEvents > 0) || (m_bdc != BinaryDataClass.RichText)) return;

			Color? oclr = UIUtil.ShowColorDialog(m_rtbText.SelectionColor);
			if(oclr.HasValue)
			{
				m_rtbText.SelectionColor = oclr.Value;
				UpdateUIState(true, true);
			}
		}

		private void OnColorBackgroundClicked(object sender, EventArgs e)
		{
			if((m_uBlockEvents > 0) || (m_bdc != BinaryDataClass.RichText)) return;

			Color? oclr = UIUtil.ShowColorDialog(m_rtbText.SelectionBackColor);
			if(oclr.HasValue)
			{
				m_rtbText.SelectionBackColor = oclr.Value;
				UpdateUIState(true, true);
			}
		}

		private void OnFileExit(object sender, EventArgs e)
		{
			this.DialogResult = DialogResult.OK;
		}

		private void OnTextTextChanged(object sender, EventArgs e)
		{
			if(m_uBlockEvents > 0) return;

			UpdateUIState(true, false);
		}

		private void OnAlignLeftClicked(object sender, EventArgs e)
		{
			if((m_uBlockEvents > 0) || (m_bdc != BinaryDataClass.RichText)) return;

			m_rtbText.SelectionAlignment = HorizontalAlignment.Left;

			UpdateUIState(true, true);
		}

		private void OnAlignCenterClicked(object sender, EventArgs e)
		{
			if((m_uBlockEvents > 0) || (m_bdc != BinaryDataClass.RichText)) return;

			m_rtbText.SelectionAlignment = HorizontalAlignment.Center;

			UpdateUIState(true, true);
		}

		private void OnAlignRightClicked(object sender, EventArgs e)
		{
			if((m_uBlockEvents > 0) || (m_bdc != BinaryDataClass.RichText)) return;

			m_rtbText.SelectionAlignment = HorizontalAlignment.Right;

			UpdateUIState(true, true);
		}

		private void OnFontComboSelectedIndexChanged(object sender, EventArgs e)
		{
			if((m_uBlockEvents > 0) || (m_bdc != BinaryDataClass.RichText)) return;

			try
			{
				string strName = m_tbFontCombo.Text;

				Font f = m_rtbText.SelectionFont;
				if(f != null)
					m_rtbText.SelectionFont = new Font(strName, f.Size,
						f.Style, f.Unit, f.GdiCharSet, f.GdiVerticalFont);
				else if(FontUtil.DefaultFont != null)
					m_rtbText.SelectionFont = FontUtil.CreateFont(strName,
						FontUtil.DefaultFont.SizeInPoints, FontStyle.Regular);
				else
					m_rtbText.SelectionFont = FontUtil.CreateFont(strName,
						12.0f, FontStyle.Regular);
			}
			catch(Exception ex) { MessageService.ShowWarning(ex); }

			UpdateUIState(true, true);
		}

		private void OnFontSizeComboSelectedIndexChanged(object sender, EventArgs e)
		{
			if((m_uBlockEvents > 0) || (m_bdc != BinaryDataClass.RichText)) return;

			try
			{
				Font f = m_rtbText.SelectionFont;
				float fSize;
				if(!float.TryParse(m_tbFontSizeCombo.Text, out fSize))
				{
					if(f != null) fSize = f.SizeInPoints;
					else if(FontUtil.DefaultFont != null)
						fSize = FontUtil.DefaultFont.SizeInPoints;
					else fSize = 12.0f;
				}

				if(f != null)
					m_rtbText.SelectionFont = new Font(f.Name, fSize, f.Style,
						GraphicsUnit.Point, f.GdiCharSet, f.GdiVerticalFont);
				else if(!NativeLib.IsUnix())
					UIUtil.RtfSetFontSize(m_rtbText, fSize);
				else // Unix
					m_rtbText.SelectionFont = FontUtil.CreateFont(
						FontFamily.GenericSansSerif, fSize, FontStyle.Regular);
			}
			catch(Exception ex) { MessageService.ShowWarning(ex); }

			UpdateUIState(true, true);
		}

		private void OnFontComboKeyDown(object sender, KeyEventArgs e)
		{
			if(e.KeyCode == Keys.Return) // Return == Enter
			{
				UIUtil.SetHandled(e, true);
				OnFontComboSelectedIndexChanged(sender, e);
			}
		}

		private void OnFontSizeComboKeyDown(object sender, KeyEventArgs e)
		{
			if(e.KeyCode == Keys.Return) // Return == Enter
			{
				UIUtil.SetHandled(e, true);
				OnFontSizeComboSelectedIndexChanged(sender, e);
			}
		}

		private void OnEditCut(object sender, EventArgs e)
		{
			m_rtbText.Cut();
			UpdateUIState(true, true);
		}

		private void OnEditCopy(object sender, EventArgs e)
		{
			m_rtbText.Copy();
			UpdateUIState(false, true);
		}

		private void OnEditPaste(object sender, EventArgs e)
		{
			m_rtbText.PasteAcceptable();
			UpdateUIState(true, true);
		}

		private void OnEditUndo(object sender, EventArgs e)
		{
			m_rtbText.Undo();
			UpdateUIState(true, true);
		}

		private void OnEditRedo(object sender, EventArgs e)
		{
			m_rtbText.Redo();
			UpdateUIState(true, true);
		}

		private void OnViewFont(object sender, EventArgs e)
		{
			using(FontDialog dlg = UIUtil.CreateFontDialog(true))
			{
				dlg.Font = Program.Config.UI.DataEditorFont.ToFont();
				dlg.ShowColor = false;

				if(dlg.ShowDialog() == DialogResult.OK)
				{
					Program.Config.UI.DataEditorFont = new AceFont(dlg.Font, true);

					if(m_bdc == BinaryDataClass.Text)
					{
						bool bModified = m_bModified; // Save modified state

						UISelectAllText(true);
						m_rtbText.SelectionFont = dlg.Font;
						UISelectAllText(false);

						m_bModified = bModified;
						UpdateUIState(false, false);
					}
				}
			}
		}

		private void OnViewWordWrap(object sender, EventArgs e)
		{
			m_rtbText.WordWrap = !m_rtbText.WordWrap;
			Program.Config.UI.DataEditorWordWrap = m_rtbText.WordWrap;
			UpdateUIState(false, false);
		}

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			bool bDown;
			if(!NativeMethods.GetKeyMessageState(ref msg, out bDown))
				return base.ProcessCmdKey(ref msg, keyData);

			if(keyData == Keys.Escape) // No modifiers
			{
				if(bDown) Close();
				return true;
			}

			return base.ProcessCmdKey(ref msg, keyData);
		}

		internal static byte[] ConvertAttachment(string strDesc, byte[] pbData)
		{
			BinaryDataClass bdc = BinaryDataClassifier.Classify(strDesc, pbData);
			if(bdc == BinaryDataClass.Text)
			{
				string strContext = (KPRes.File + (string.IsNullOrEmpty(strDesc) ?
					string.Empty : (": " + strDesc)));

				TextEncodingForm dlg = new TextEncodingForm();
				dlg.InitEx(strContext, pbData);
				if(UIUtil.ShowDialogNotValue(dlg, DialogResult.OK)) return null;

				Encoding enc = dlg.SelectedEncoding;
				int iStart = (int)dlg.DataStartOffset;
				UIUtil.DestroyForm(dlg);
				if(enc != null)
				{
					try
					{
						string strText = (enc.GetString(pbData, iStart,
							pbData.Length - iStart) ?? string.Empty);
						strText = StrUtil.ReplaceNulls(strText);
						return StrUtil.Utf8.GetBytes(strText);
					}
					catch(Exception) { Debug.Assert(false); }
				}
			}

			return pbData;
		}

		private void OnTextFindKeyDown(object sender, KeyEventArgs e)
		{
			if(e.KeyCode == Keys.Return) // Return == Enter
			{
				UIUtil.SetHandled(e, true);
				PerformQuickFind();
			}
		}

		private void OnTextFindKeyUp(object sender, KeyEventArgs e)
		{
			if(e.KeyCode == Keys.Return) // Return == Enter
				UIUtil.SetHandled(e, true);
		}

		private void PerformQuickFind()
		{
			string strNeedle = m_tbFind.Text;
			if(string.IsNullOrEmpty(strNeedle)) return;

			int iStart = m_rtbText.SelectionStart + m_rtbText.SelectionLength;
			if(iStart < 0) { Debug.Assert(false); iStart = 0; }
			if(iStart >= m_rtbText.TextLength) iStart = 0;

			int p = m_rtbText.Find(strNeedle, iStart, -1, RichTextBoxFinds.None);
			if(p < 0) m_rtbText.Find(strNeedle, 0, -1, RichTextBoxFinds.None);
		}

		private void OnEditSelectAll(object sender, EventArgs e)
		{
			m_rtbText.SelectAll();
		}

		private void OnEditFind(object sender, EventArgs e)
		{
			m_tbFind.SelectAll();
			UIUtil.SetFocus(m_tbFind.Control, this, true);
		}

		private void OnEditDelete(object sender, EventArgs e)
		{
			m_rtbText.SelectedText = string.Empty;
		}
	}
}
