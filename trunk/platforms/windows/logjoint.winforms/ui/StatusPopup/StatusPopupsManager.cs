﻿using System.Windows.Forms;
using System.Drawing;
using LogJoint.UI.Presenters.StatusReports;

namespace LogJoint.UI
{
	class StatusPopupsManager : IPresenter
	{
		internal StatusPopup activeStatusReport;
		internal StatusPopup autoHideStatusReport;
		internal InfoPopupControl infoPopup;
		internal ToolStripItem toolStripStatusLabel;
		internal Form appWindow;

		public StatusPopupsManager(Form appWindow, ToolStripItem toolStripStatusLabel, IHeartBeatTimer heartbeatTimer)
		{
			this.appWindow = appWindow;
			this.toolStripStatusLabel = toolStripStatusLabel;

			infoPopup = new InfoPopupControl();
			appWindow.Controls.Add(infoPopup);
			infoPopup.Location = new Point(appWindow.ClientSize.Width - infoPopup.Width, appWindow.ClientSize.Height - infoPopup.Height);
			infoPopup.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
			infoPopup.BringToFront();

			heartbeatTimer.OnTimer += (s, e) => Timeslice();
		}

		IReport IPresenter.CreateNewStatusReport()
		{
			if (activeStatusReport != null)
				activeStatusReport.Dispose();
			activeStatusReport = new StatusPopup(this);
			return activeStatusReport;
		}

		void Timeslice()
		{
			if (autoHideStatusReport != null)
			{
				autoHideStatusReport.AutoHideIfItIsTime();
			}
		}
	}
}
