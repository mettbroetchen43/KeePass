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
using System.Text;
using System.Windows.Forms;

using KeePass.Native;

namespace KeePass.UI
{
	public sealed class CustomMenuStripEx : MenuStrip
	{
		public CustomMenuStripEx() : base()
		{
			// ThemeToolStripRenderer.AttachTo(this);

			UIUtil.Configure(this);
		}

		protected override void WndProc(ref Message m)
		{
			base.WndProc(ref m);

			// Enable 'click through' behavior
			if((m.Msg == NativeMethods.WM_MOUSEACTIVATE) &&
				(m.Result == (IntPtr)NativeMethods.MA_ACTIVATEANDEAT))
			{
				m.Result = (IntPtr)NativeMethods.MA_ACTIVATE;
			}
		}

		// protected override void OnItemClicked(ToolStripItemClickedEventArgs e)
		// {
		//	if(UIUtil.HasClickedSeparator(e)) return; // Ignore the click
		//	base.OnItemClicked(e);
		// }

		// protected override void OnMouseDown(MouseEventArgs mea)
		// {
		//	ToolStripSeparator s = (GetItemAt(mea.X, mea.Y) as ToolStripSeparator);
		//	if(s != null) return;
		//	base.OnMouseDown(mea);
		// }

		// protected override void OnMouseUp(MouseEventArgs mea)
		// {
		//	ToolStripSeparator s = (GetItemAt(mea.X, mea.Y) as ToolStripSeparator);
		//	if(s != null) return;
		//	base.OnMouseUp(mea);
		// }

		// protected override void OnMouseClick(MouseEventArgs e)
		// {
		//	ToolStripSeparator s = (GetItemAt(e.X, e.Y) as ToolStripSeparator);
		//	if(s != null) return;
		//	base.OnMouseClick(e);
		// }
	}
}
