﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace LogJoint.UI
{
	public partial class SearchTextBox : ComboBox
	{
		public SearchTextBox()
		{
			InitializeComponent();
		}

		public EventHandler Search;
		public EventHandler Escape;

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (keyData == Keys.Enter)
			{
				this.DroppedDown = false;
				if (Search != null)
					Search(this, EventArgs.Empty);
				return true;
			}
			else if (keyData == Keys.Escape)
			{
				if (Escape != null)
					Escape(this, EventArgs.Empty);
				return true;
			}
			else if (keyData == (Keys.Control | Keys.F))
			{
				return true;
			}
			return base.ProcessCmdKey(ref msg, keyData);
		}
	}
}
