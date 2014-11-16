using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using LogJoint.RegularExpressions;
using System.Runtime.InteropServices;
using LogJoint.UI.Presenters.LogViewer;
using System.Linq;

namespace LogJoint.UI
{
	public partial class LogViewerControl : Control, IView
	{
		#region Public interface

		public LogViewerControl()
		{
			InitializeComponent();

			this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);

			base.BackColor = Color.White;
			base.TabStop = true;
			base.Enabled = true;

			bufferedGraphicsContext = new BufferedGraphicsContext() { MaximumBuffer = new Size(5000, 4000) };

			var prototypeStringFormat = StringFormat.GenericDefault;
			drawContext.TextFormat = (StringFormat)prototypeStringFormat.Clone();
			drawContext.TextFormat.SetTabStops(0, new float[] { 20 });
			drawContext.TextFormat.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;


			drawContext.OutlineMarkupPen = new Pen(Color.Gray, 1);
			drawContext.SelectedOutlineMarkupPen = new Pen(Color.White, 1);

			drawContext.InfoMessagesBrush = SystemBrushes.ControlText;
			drawContext.SelectedTextBrush = SystemBrushes.HighlightText;
			drawContext.SelectedFocuslessTextBrush = SystemBrushes.ControlText;
			drawContext.CommentsBrush = SystemBrushes.GrayText;

			drawContext.DefaultBackgroundBrush = SystemBrushes.Window;
			drawContext.SelectedBkBrush = new SolidBrush(Color.FromArgb(167, 176, 201));
			drawContext.SelectedFocuslessBkBrush = Brushes.Gray;

			drawContext.FocusedMessageBkBrush = new SolidBrush(Color.FromArgb(167 + 30, 176 + 30, 201 + 30));

			drawContext.ErrorIcon = errPictureBox.Image;
			drawContext.WarnIcon = warnPictureBox.Image;
			drawContext.BookmarkIcon = bookmarkPictureBox.Image;
			drawContext.SmallBookmarkIcon = smallBookmarkPictureBox.Image;
			drawContext.FocusedMessageIcon = focusedMessagePictureBox.Image;
			drawContext.FocusedMessageSlaveIcon = focusedMessageSlavePictureBox.Image;

			drawContext.HighlightPen = new Pen(Color.Red, 3);
			drawContext.HighlightPen.LineJoin = LineJoin.Round;

			drawContext.CursorPen = new Pen(Color.Black, 2);

			drawContext.TimeSeparatorLine = new Pen(Color.Gray, 1);

			drawContext.HighlightBrush = Brushes.Cyan;

			int hightlightingAlpha = 170;
			drawContext.InplaceHightlightBackground1 =
				new SolidBrush(Color.FromArgb(hightlightingAlpha, Color.LightSalmon));
			drawContext.InplaceHightlightBackground2 =
				new SolidBrush(Color.FromArgb(hightlightingAlpha, Color.Cyan));

			drawContext.RightCursor = new System.Windows.Forms.Cursor(
				System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("LogJoint.ui.LogViewerControl.cursor_r.cur"));

			drawContext.ClientRect = this.ClientRectangle;

			EnsureBackbufferIsUpToDate();
			UpdateFontSizeDependentData();

			cursorTimer.Tick += (s, e) =>
			{
				drawContext.CursorState = !drawContext.CursorState;
				if (presenter != null)
					presenter.InvalidateTextLineUnderCursor();
			};

			animationTimer.Tick += (s, e) =>
			{
				if (drawContext.SlaveMessagePositionAnimationStep < 8)
				{
					drawContext.SlaveMessagePositionAnimationStep++;
				}
				else
				{
					animationTimer.Enabled = false;
					drawContext.SlaveMessagePositionAnimationStep = 0;
				}
				Invalidate();
			};
		}

		public void SetPresenter(Presenter presenter)
		{
			this.presenter = presenter;
			this.tracer = presenter.Tracer;
			drawContext.Presenter = presenter;
		}

		public Presenter Presenter { get { return presenter; } }

		#endregion

		#region IView members

		void IView.UpdateStarted()
		{
			if (presenterUpdate != null)
				return;

			presenterUpdate = new PresenterUpdate()
			{
				FocusedBeforeUpdate = presenter.FocusedMessage,
				RelativeForcusedScrollPositionBeforeUpdate = selection.DisplayPosition * drawContext.LineHeight - scrollBarsInfo.scrollPos.Y
			};
		}

		void IView.UpdateFinished()
		{
			if (presenterUpdate == null)
				return;
			try
			{
				if (presenterUpdate.FocusedBeforeUpdate != null
				 && presenter.FocusedMessage != null)
				{
					SetScrollPos(new Point(scrollBarsInfo.scrollPos.X,
						selection.DisplayPosition * drawContext.LineHeight - presenterUpdate.RelativeForcusedScrollPositionBeforeUpdate));
				}
				else
				{
					SetScrollPos(new Point(scrollBarsInfo.scrollPos.X, 0));
				}
			}
			finally
			{
				presenterUpdate = null;
			}
		}

		void IView.InvalidateMessage(Presenter.DisplayLine line)
		{
			Rectangle r = DrawingUtils.GetMetrics(line, drawContext).MessageRect;
			this.Invalidate(r);
		}

		void IView.ScrollInView(int messageDisplayPosition, bool showExtraLinesAroundMessage)
		{
			if (scrollBarsInfo.userIsScrolling)
			{
				return;
			}

			int? newScrollPos = null;

			VisibleMessages vl = GetVisibleMessages(ClientRectangle);

			int extra = showExtraLinesAroundMessage ? 2 : 0;

			if (messageDisplayPosition < vl.fullyVisibleBegin + extra)
				newScrollPos = messageDisplayPosition - extra;
			else if (messageDisplayPosition > vl.fullyVisibleEnd - extra)
				newScrollPos = messageDisplayPosition  - (vl.fullyVisibleEnd - vl.begin) + extra;

			if (newScrollPos.HasValue)
				SetScrollPos(new Point(scrollBarsInfo.scrollPos.X, newScrollPos.Value * drawContext.LineHeight));
		}

		void IView.UpdateScrollSizeToMatchVisibleCount()
		{
			SetScrollSize(new Size(10, GetDisplayMessagesCount() * drawContext.LineHeight), true, false);
		}

		IEnumerable<Presenter.DisplayLine> IView.GetVisibleMessagesIterator()
		{
			return GetVisibleMessagesIterator(ClientRectangle);
		}

		void IView.HScrollToSelectedText()
		{
			if (selection.First.Message == null)
				return;

			int pixelThatMustBeVisible = (int)(selection.First.LineCharIndex * drawContext.CharSize.Width);
			if (drawContext.ShowTime)
				pixelThatMustBeVisible += drawContext.TimeAreaSize;

			int currentVisibleLeft = scrollBarsInfo.scrollPos.X;
			int currentVisibleRight = scrollBarsInfo.scrollPos.X + drawContext.ClientRect.Width - SystemInformation.VerticalScrollBarWidth;
			int extraPixelsAroundSelection = 20;
			if (pixelThatMustBeVisible < scrollBarsInfo.scrollPos.X)
			{
				SetScrollPos(new Point(pixelThatMustBeVisible - extraPixelsAroundSelection, scrollBarsInfo.scrollPos.Y));
			}
			if (pixelThatMustBeVisible >= currentVisibleRight)
			{
				SetScrollPos(new Point(scrollBarsInfo.scrollPos.X + (pixelThatMustBeVisible - currentVisibleRight + extraPixelsAroundSelection), 
					scrollBarsInfo.scrollPos.Y));
			}
		}

		void IView.RestartCursorBlinking()
		{
			drawContext.CursorState = true;
		}

		void IView.SetClipboard(string text)
		{
			try
			{
				Clipboard.SetText(text);
			}
			catch (Exception)
			{
				MessageBox.Show("Failed to copy data to the clipboard");
			}
		}

		void IView.DisplayEverythingFilteredOutMessage(bool displayOrHide)
		{
			if (everythingFilteredOutMessage == null)
			{
				everythingFilteredOutMessage = new EverythingFilteredOutMessage();
				everythingFilteredOutMessage.Visible = displayOrHide;
				Controls.Add(everythingFilteredOutMessage);
				everythingFilteredOutMessage.Dock = DockStyle.Fill;
				everythingFilteredOutMessage.FiltersLinkLabel.Click += (s, e) => presenter.OnShowFiltersClicked();
				everythingFilteredOutMessage.SearchUpLinkLabel.Click +=
					(s, e) => presenter.Search(new SearchOptions() { CoreOptions = new Search.Options() { ReverseSearch = true }, HighlightResult = false });
				everythingFilteredOutMessage.SearchDownLinkLabel.Click +=
					(s, e) => presenter.Search(new SearchOptions() { CoreOptions = new Search.Options() { ReverseSearch = false }, HighlightResult = false });
			}
			else
			{
				everythingFilteredOutMessage.Visible = displayOrHide;
			}
		}

		void IView.DisplayNothingLoadedMessage(string messageToDisplayOrNull)
		{
			if (string.IsNullOrWhiteSpace(messageToDisplayOrNull))
				messageToDisplayOrNull = null;
			if (emptyMessagesCollectionMessage == null)
			{
				emptyMessagesCollectionMessage = new EmptyMessagesCollectionMessage();
				emptyMessagesCollectionMessage.Visible = messageToDisplayOrNull != null;
				Controls.Add(emptyMessagesCollectionMessage);
				emptyMessagesCollectionMessage.Dock = DockStyle.Fill;
			}
			else
			{
				emptyMessagesCollectionMessage.Visible = messageToDisplayOrNull != null;
			}
			if (messageToDisplayOrNull != null)
				emptyMessagesCollectionMessage.SetMessage(messageToDisplayOrNull);
		}

		void IView.PopupContextMenu(object contextMenuPopupData)
		{
			Point pt;
			if (contextMenuPopupData is Point)
				pt = (Point)contextMenuPopupData;
			else
				pt = new Point();
			DoContextMenu(pt.X, pt.Y);
		}

		void IView.OnFontSizeChanged()
		{
			UpdateFontSizeDependentData();
		}

		void IView.OnShowMillisecondsChanged()
		{
			UpdateTimeAreaSize();
		}

		int IView.DisplayLinesPerPage { get { return Height / drawContext.LineHeight; } }

		object IView.GetContextMenuPopupDataForCurrentSelection()
		{
			return new Point(0, (selection.DisplayPosition + 1) * drawContext.LineHeight - 1 - drawContext.ScrollPos.Y);
		}

		void IView.OnColoringChanged()
		{
			Invalidate();
		}

		void IView.OnSlaveMessageChanged()
		{
			Invalidate();
		}

		void IView.AnimateSlaveMessagePosition()
		{
			drawContext.SlaveMessagePositionAnimationStep = 0;
			animationTimer.Enabled = true;
			Invalidate();
		}

		#endregion

		#region Overriden event handlers

		protected override void OnMouseWheel(MouseEventArgs e)
		{
			if (presenter == null)
				return;

			if ((Control.ModifierKeys & Keys.Control) != 0)
			{
				if (e.Delta > 0 && presenter.FontSize != Presenters.LogViewer.Presenter.LogFontSize.Maximum)
					presenter.FontSize = presenter.FontSize + 1;
				else if (e.Delta < 0 && presenter.FontSize != Presenters.LogViewer.Presenter.LogFontSize.Minimum)
					presenter.FontSize = presenter.FontSize - 1;
			}
			else
			{
				Rectangle clientRectangle = base.ClientRectangle;
				int p = drawContext.ScrollPos.Y - e.Delta;
				SetScrollPos(new Point(scrollBarsInfo.scrollPos.X, p));
				if (e is HandledMouseEventArgs)
				{
					((HandledMouseEventArgs)e).Handled = true;
				}
			}
			base.OnMouseWheel(e);
		}

		protected override void OnMouseDoubleClick(MouseEventArgs e)
		{
			foreach (var i in GetVisibleMessagesIterator(ClientRectangle))
			{
				IMessage l = i.Message;
				var m = DrawingUtils.GetMetrics(i, drawContext);
				if (!m.MessageRect.Contains(e.X, e.Y))
					continue;
				if (m.OulineBox.Contains(e.X, e.Y))
					continue;
				//var hitTester = new HitTestingVisitor(drawContext, preprocessedMessage, e.Location.X, i.TextLineIndex);
				//i.Message.Visit(hitTester);
				break;
			}
			base.OnMouseDoubleClick(e);
		}

		protected override void OnGotFocus(EventArgs e)
		{
			InvalidateMessagesArea();
			base.OnGotFocus(e);
		}

		protected override void OnLostFocus(EventArgs e)
		{
			InvalidateMessagesArea();
			base.OnLostFocus(e);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			this.Focus();

			bool captureTheMouse = true;

			foreach (var i in GetVisibleMessagesIterator(ClientRectangle))
			{
				DrawingUtils.Metrics mtx = DrawingUtils.GetMetrics(i, drawContext);

				// if used clicked line'factors outline box (collapse/expand cross)
				if (i.Message.IsStartFrame && mtx.OulineBox.Contains(e.X, e.Y) && i.TextLineIndex == 0)
					if (presenter.OulineBoxClicked(i.Message, ModifierKeys == Keys.Control))
					{
						captureTheMouse = false;
						break;
					}

				// if user clicked line area
				if (mtx.MessageRect.Contains(e.X, e.Y))
				{
					var hitTester = new HitTestingVisitor(drawContext, mtx, e.Location.X, i.TextLineIndex);
					i.Message.Visit(hitTester);

					Presenter.MessageRectClickFlag flags = Presenter.MessageRectClickFlag.None;
					if (e.Button == MouseButtons.Right)
						flags |= Presenter.MessageRectClickFlag.RightMouseButton;
					if (Control.ModifierKeys == Keys.Shift)
						flags |= Presenter.MessageRectClickFlag.ShiftIsHeld;
					if (Control.ModifierKeys == Keys.Alt)
						flags |= Presenter.MessageRectClickFlag.AltIsHeld;
					if (e.Clicks == 2)
						flags |= Presenter.MessageRectClickFlag.DblClick;
					if (e.X < FixedMetrics.CollapseBoxesAreaSize)
						flags |= Presenter.MessageRectClickFlag.OulineBoxesAreaClicked;
					presenter.MessageRectClicked(CursorPosition.FromDisplayLine(i, hitTester.LineTextPosition), flags, e.Location);
					break;
				}
			}

			base.OnMouseDown(e);
			
			this.Capture = captureTheMouse;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			Cursor newCursor = Cursors.Arrow;

			foreach (var i in GetVisibleMessagesIterator(ClientRectangle))
			{
				DrawingUtils.Metrics mtx = DrawingUtils.GetMetrics(i, drawContext);

				if (e.Y >= mtx.MessageRect.Top && e.Y < mtx.MessageRect.Bottom)
				{
					if (e.Button == MouseButtons.Left && this.Capture)
					{
						var hitTester = new HitTestingVisitor(drawContext, mtx, e.Location.X, i.TextLineIndex);
						i.Message.Visit(hitTester);
						Presenter.MessageRectClickFlag flags = Presenter.MessageRectClickFlag.ShiftIsHeld;
						if (e.X < FixedMetrics.CollapseBoxesAreaSize)
							flags |= Presenter.MessageRectClickFlag.OulineBoxesAreaClicked;
						presenter.MessageRectClicked(CursorPosition.FromDisplayLine(i, hitTester.LineTextPosition),
							flags, e.Location);
					}
					if (i.Message.IsStartFrame && mtx.OulineBox.Contains(e.Location))
						newCursor = Cursors.Arrow;
					else if (e.X < FixedMetrics.CollapseBoxesAreaSize)
						newCursor = drawContext.RightCursor;
					else if (e.X >= drawContext.GetTextOffset(0, 0).X)
						newCursor = Cursors.IBeam;
					else
						newCursor = Cursors.Arrow;
				}
			}

			if (Cursor != newCursor)
				Cursor = newCursor;

			base.OnMouseMove(e);
		}

		protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e)
		{
			if (e.Modifiers == Keys.Shift && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Left || e.KeyCode == Keys.Right))
			{
				e.IsInputKey = true;
			}
			base.OnPreviewKeyDown(e);
		}

		protected override bool IsInputKey(Keys keyData)
		{
			if (keyData == Keys.Up || keyData == Keys.Down
					|| keyData == Keys.Left || keyData == Keys.Right)
				return true;
			return base.IsInputKey(keyData);
		}

		protected override void OnKeyDown(KeyEventArgs kevent)
		{
			Keys k = kevent.KeyCode;
			bool ctrl = (kevent.Modifiers & Keys.Control) != 0;
			bool alt = (kevent.Modifiers & Keys.Alt) != 0;
			bool shift = (kevent.Modifiers & Keys.Shift) != 0;

			Presenter.Key pk;

			if (k == Keys.F5)
				pk = Presenter.Key.F5;
			else if (k == Keys.Up)
				pk = Presenter.Key.Up;
			else if (k == Keys.Down)
				pk = Presenter.Key.Down;
			else if (k == Keys.PageUp)
				pk = Presenter.Key.PageUp;
			else if (k == Keys.PageDown)
				pk = Presenter.Key.PageDown;
			else if (k == Keys.Left)
				pk = Presenter.Key.Left;
			else if (k == Keys.Right)
				pk = Presenter.Key.Right;
			else if (k == Keys.Apps)
				pk = Presenter.Key.Apps;
			else if (k == Keys.Enter)
				pk = Presenter.Key.Enter;
			else if (k == Keys.C && ctrl)
				pk = Presenter.Key.Copy;
			else if (k == Keys.Insert && ctrl)
				pk = Presenter.Key.Copy;
			else if (k == Keys.Home)
				pk = Presenter.Key.Home;
			else if (k == Keys.End)
				pk = Presenter.Key.End;
			else
				pk = Presenter.Key.None;

			if (presenter != null && pk != Presenter.Key.None)
				presenter.KeyPressed(pk, ctrl, alt, shift);

			base.OnKeyDown(kevent);
		}

		protected override void OnPaintBackground(PaintEventArgs pevent)
		{
			// Do nothing. All painting is in OnPaint.
		}

		protected override void OnPaint(PaintEventArgs pe)
		{
			DrawContext dc = drawContext;

			dc.Canvas.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
			dc.Canvas.FillRectangle(dc.DefaultBackgroundBrush, dc.ClientRect);

			int maxRight = 0;

			var sel = selection;
			bool needToDrawCursor = drawContext.CursorState == true && Focused && sel.First.Message != null;

			var drawingVisitor = new DrawingVisitor();
			drawingVisitor.ctx = dc;
			drawingVisitor.InplaceHighlightHandler1 = presenter != null ? presenter.InplaceHighlightHandler1 : null;
			drawingVisitor.InplaceHighlightHandler2 = presenter != null ? presenter.InplaceHighlightHandler2 : null;

			var messagesToDraw = GetVisibleMessages(pe.ClipRectangle);

			var displayLinesEnum = presenter != null ? presenter.GetDisplayLines(messagesToDraw.begin, messagesToDraw.end) : Enumerable.Empty<Presenter.DisplayLine>();
			foreach (var il in displayLinesEnum)
			{
				DrawingUtils.Metrics m = DrawingUtils.GetMetrics(il, dc);
				drawingVisitor.m = m;
				drawingVisitor.DisplayIndex = il.DisplayLineIndex;
				drawingVisitor.TextLineIdx = il.TextLineIndex;
				if (needToDrawCursor && sel.First.DisplayIndex == il.DisplayLineIndex)
					drawingVisitor.CursorPosition = sel.First;
				else
					drawingVisitor.CursorPosition = null;

				il.Message.Visit(drawingVisitor);

				maxRight = Math.Max(maxRight, m.OffsetTextRect.Right);
			}

			DrawFocusedMessageMark(messagesToDraw);

			dc.BackBufferCanvas.Render(pe.Graphics);

			UpdateScrollSize(dc, maxRight);

			base.OnPaint(pe);
		}

		private void DrawFocusedMessageMark(VisibleMessages messagesToDraw)
		{
			if (presenter == null)
				return;
			var dc = drawContext;
			Image focusedMessageMark = null;
			int markYPos = 0;
			if (presenter.FocusedMessageDisplayMode == Presenter.FocusedMessageDisplayModes.Master)
			{
				var sel = selection;
				if (sel.First.Message != null)
				{
					focusedMessageMark = dc.FocusedMessageIcon;
					markYPos = dc.GetTextOffset(0, sel.First.DisplayIndex).Y + (dc.LineHeight - focusedMessageMark.Height) / 2;
				}
			}
			else
			{
				if (presenter.VisibleMessagesCount != 0)
				{
					var slaveModeFocusInfo = presenter.FindSlaveModeFocusedMessagePosition(
						Math.Max(messagesToDraw.begin - 4, 0),
						Math.Min(messagesToDraw.end + 4, presenter.VisibleMessagesCount));
					if (slaveModeFocusInfo != null)
					{
						focusedMessageMark = dc.FocusedMessageSlaveIcon;
						int yOffset = slaveModeFocusInfo.Item1 != slaveModeFocusInfo.Item2 ?
							(dc.LineHeight - focusedMessageMark.Height) / 2 : -focusedMessageMark.Height / 2;
						markYPos = dc.GetTextOffset(0, slaveModeFocusInfo.Item1).Y + yOffset;
					}
				}
			}
			if (focusedMessageMark != null)
			{
				var gs = dc.Canvas.Save();
				dc.Canvas.TranslateTransform(
					FixedMetrics.CollapseBoxesAreaSize - focusedMessageMark.Width / 2 + 1,
					markYPos + focusedMessageMark.Height / 2);
				var imageToDraw = focusedMessageMark;
				if (dc.SlaveMessagePositionAnimationStep > 0)
				{
					var factors = new float[] { .81f, 1f, 0.9f, .72f, .54f, .36f, .18f, .09f };
					float factor = 1f + 1.4f * factors[dc.SlaveMessagePositionAnimationStep-1];
					dc.Canvas.ScaleTransform(factor, factor);
					imageToDraw = dc.FocusedMessageIcon;
				}
				dc.Canvas.DrawImage(
					imageToDraw,
					-focusedMessageMark.Width/2,
					-focusedMessageMark.Height/2,
					focusedMessageMark.Width,
					focusedMessageMark.Height);
				dc.Canvas.Restore(gs);
			}
		}

		protected override void OnResize(EventArgs e)
		{
			drawContext.ClientRect = this.ClientRectangle;
			EnsureBackbufferIsUpToDate();
			SetScrollPos(scrollBarsInfo.scrollPos);
			Invalidate();
			base.OnResize(e);
		}

		protected override void WndProc(ref System.Windows.Forms.Message m)
		{
			switch (m.Msg)
			{
				case 0x114:
					this.WmHScroll(ref m);
					return;

				case 0x115:
					this.WmVScroll(ref m);
					return;

				case ScrollBarsInfo.WM_REPAINTSCROLLBARS:
					this.WMRepaintScrollBars();
					return;
			}
			base.WndProc(ref m);
		}

		protected override void OnLayout(LayoutEventArgs levent)
		{
			this.UpdateScrollBars(true, true);
			base.OnLayout(levent);
		}

		protected override CreateParams CreateParams
		{
			get
			{
				CreateParams createParams = base.CreateParams;
				createParams.Style |= 0x100000; // horz scroll
				createParams.Style |= 0x200000; // vert scroll
				return createParams;
			}
		}

		#endregion

		#region Implementation

		static FontFamily InitializeFontFamily()
		{
			var families = FontFamily.Families;
			var preferredFamilies = new string[] {"consolas", "courier new", "courier"};
			foreach (var preferredFamily in preferredFamilies)
			{
				var installedFamily = families.FirstOrDefault(f => string.Compare(f.Name, preferredFamily, true) == 0);
				if (installedFamily != null)
					return installedFamily;
			}
			return FontFamily.GenericMonospace;
		}

		void UpdateFontSizeDependentData()
		{
			if (drawContext.Font != null)
				drawContext.Font.Dispose();

			float emSize;
			switch (presenter != null ? presenter.FontSize : Presenters.LogViewer.Presenter.LogFontSize.Normal)
			{
				case Presenters.LogViewer.Presenter.LogFontSize.ExtraSmall: emSize = 7; break;
				case Presenters.LogViewer.Presenter.LogFontSize.Small: emSize = 8; break;
				case Presenters.LogViewer.Presenter.LogFontSize.Large1: emSize = 10; break;
				case Presenters.LogViewer.Presenter.LogFontSize.Large2: emSize = 11; break;
				case Presenters.LogViewer.Presenter.LogFontSize.Large3: emSize = 14; break;
				case Presenters.LogViewer.Presenter.LogFontSize.Large4: emSize = 16; break;
				case Presenters.LogViewer.Presenter.LogFontSize.Large5: emSize = 18; break;
				default: emSize = 9; break;
			}
			drawContext.Font = new Font(fontFamily.Value, emSize);

			using (Graphics tmp = Graphics.FromHwnd(IntPtr.Zero))
			{
				int count = 8 * 1024;
				drawContext.CharSize = tmp.MeasureString(new string('0', count), drawContext.Font);
				drawContext.CharWidthDblPrecision = (double)drawContext.CharSize.Width / (double)count;
				drawContext.CharSize.Width /= (float)count;
				drawContext.LineHeight = (int)Math.Floor(drawContext.CharSize.Height);
			}

			UpdateTimeAreaSize();
		}

		void UpdateTimeAreaSize()
		{
			string testStr = (new MessageTimestamp(new DateTime(2011, 11, 11, 11, 11, 11, 111))).ToUserFrendlyString(drawContext.ShowMilliseconds);
			drawContext.TimeAreaSize = (int)Math.Floor(
				drawContext.CharSize.Width * (float)testStr.Length
			) + 10;
		}

		struct VisibleMessages
		{
			public int begin;
			public int end;
			public int fullyVisibleBegin;
			public int fullyVisibleEnd;
		};

		VisibleMessages GetVisibleMessages(Rectangle viewRect)
		{
			VisibleMessages rv;
			
			viewRect.Offset(0, drawContext.ScrollPos.Y);

			rv.begin = viewRect.Y / drawContext.LineHeight;
			rv.fullyVisibleBegin = rv.begin;
			if ((viewRect.Y % drawContext.LineHeight) != 0)
				++rv.fullyVisibleBegin;

			rv.end = viewRect.Bottom / drawContext.LineHeight;
			rv.fullyVisibleEnd = rv.end;
			--rv.fullyVisibleEnd;
			if ((viewRect.Bottom % drawContext.LineHeight) != 0)
				++rv.end;
			
			int visibleCount = GetDisplayMessagesCount();
			rv.begin = Math.Min(visibleCount, rv.begin);
			rv.end = Math.Min(visibleCount, rv.end);
			rv.fullyVisibleEnd = Math.Min(visibleCount, rv.fullyVisibleEnd);

			return rv;
		}

		IEnumerable<Presenter.DisplayLine> GetVisibleMessagesIterator(Rectangle viewRect)
		{
			if (presenter == null)
				return Enumerable.Empty<Presenter.DisplayLine>();
			VisibleMessages vl = GetVisibleMessages(viewRect);
			return presenter.GetDisplayLines(vl.begin, vl.end);
		}		

		void DoContextMenu(int x, int y)
		{
			contextMenuStrip1.Show(this, x, y);
		}

		void contextMenuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
		{
			if (e.ClickedItem == this.copyMenuItem)
				presenter.CopySelectionToClipboard();
			else if (e.ClickedItem == this.collapseMenuItem)
				presenter.DoExpandCollapse(selection.Message, false, new bool?());
			else if (e.ClickedItem == this.recursiveCollapseMenuItem)
				presenter.DoExpandCollapse(selection.Message, true, new bool?());
			else if (e.ClickedItem == this.gotoParentFrameMenuItem)
				presenter.GoToParentFrame();
			else if (e.ClickedItem == this.gotoEndOfFrameMenuItem)
				presenter.GoToEndOfFrame();
			else if (e.ClickedItem == this.showTimeMenuItem)
				presenter.ShowTime = (showTimeMenuItem.Checked = !showTimeMenuItem.Checked);
			else if (e.ClickedItem == this.showRawMessagesMenuItem)
				presenter.ShowRawMessages = (showRawMessagesMenuItem.Checked = !showRawMessagesMenuItem.Checked);
			else if (e.ClickedItem == this.defaultActionMenuItem)
				presenter.PerformDefaultFocusedMessageAction();
			else if (e.ClickedItem == this.toggleBmkStripMenuItem)
				presenter.ToggleBookmark(selection.Message);
			else if (e.ClickedItem == this.gotoNextMessageInTheThreadMenuItem)
				presenter.GoToNextMessageInThread();
			else if (e.ClickedItem == this.gotoPrevMessageInTheThreadMenuItem)
				presenter.GoToPrevMessageInThread();
			else if (e.ClickedItem == this.collapseAlllFramesMenuItem)
				presenter.CollapseOrExpandAllFrames(true);
			else if (e.ClickedItem == this.expandAllFramesMenuItem)
				presenter.CollapseOrExpandAllFrames(false);
		}

		void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
		{
			showTimeMenuItem.Checked = presenter.ShowTime;
			showRawMessagesMenuItem.Visible = presenter.RawViewAllowed;
			showRawMessagesMenuItem.Checked = presenter.ShowRawMessages;
			toggleBmkStripMenuItem.Visible = presenter.BookmarksAvailable;
			bool collapseExpandVisible = presenter != null && presenter.Selection.Message != null && presenter.Selection.Message.IsStartFrame;
			collapseMenuItem.Visible = collapseExpandVisible;
			recursiveCollapseMenuItem.Visible = collapseExpandVisible;

			string defaultAction = presenter.DefaultFocusedMessageActionCaption;
			defaultActionMenuItem.Visible = !string.IsNullOrEmpty(defaultAction);
			defaultActionMenuItem.Text = defaultAction;
		}

		void UpdateScrollSize(DrawContext dc, int maxRight)
		{
			maxRight += dc.ScrollPos.X;
			if (maxRight > scrollBarsInfo.scrollSize.Width)
			{
				SetScrollSize(new Size(maxRight, scrollBarsInfo.scrollSize.Height), false, true);
			}
		}

		void EnsureBackbufferIsUpToDate()
		{
			var clientSize = drawContext.ClientRect.Size;
			if (drawContext.BackBufferCanvas == null
			 || clientSize.Width > drawContext.BackBufferCanvasSize.Width
			 || clientSize.Height > drawContext.BackBufferCanvasSize.Height)
			{
				if (drawContext.BackBufferCanvas != null)
					drawContext.BackBufferCanvas.Dispose();
				using (var tmp = this.CreateGraphics())
					drawContext.BackBufferCanvas = bufferedGraphicsContext.Allocate(tmp, new Rectangle(0, 0, clientSize.Width, clientSize.Height));
			}
		}

		void InvalidateMessagesArea()
		{
			Rectangle r = ClientRectangle;
			r.X += FixedMetrics.CollapseBoxesAreaSize;
			r.Width -= FixedMetrics.CollapseBoxesAreaSize;
			Invalidate(r);
		}

		void SetScrollPos(Point pos)
		{
			if (pos.Y > scrollBarsInfo.scrollSize.Height)
				pos.Y = scrollBarsInfo.scrollSize.Height;
			else if (pos.Y < 0)
				pos.Y = 0;

			int maxPosX = Math.Max(0, scrollBarsInfo.scrollSize.Width - ClientSize.Width);

			if (pos.X > maxPosX)
				pos.X = maxPosX;
			else if (pos.X < 0)
				pos.X = 0;

			int xBefore = GetScrollInfo(Native.SB.HORZ).nPos;
			int yBefore = GetScrollInfo(Native.SB.VERT).nPos;

			bool vRedraw = pos.Y != scrollBarsInfo.scrollPos.Y;
			bool hRedraw = pos.X != scrollBarsInfo.scrollPos.X;
			scrollBarsInfo.scrollPos = pos;
			UpdateScrollBars(vRedraw, hRedraw);

			int xDelta = xBefore - GetScrollInfo(Native.SB.HORZ).nPos;
			int yDelta = yBefore - GetScrollInfo(Native.SB.VERT).nPos;

			if (xDelta == 0 && yDelta == 0)
			{
			}
			else if (xDelta != 0 && yDelta != 0)
			{
				Invalidate();
			}
			else
			{
				Rectangle r = drawContext.ClientRect;
				if (xDelta != 0)
				{
					r.X += FixedMetrics.CollapseBoxesAreaSize;
					r.Width -= FixedMetrics.CollapseBoxesAreaSize;
				}
				Native.RECT scroll = new Native.RECT(r);
				Native.RECT clip = scroll;
				Native.RECT update = scroll;
				HandleRef hrgnUpdate = new HandleRef(null, IntPtr.Zero);
				Native.ScrollWindowEx(
					new HandleRef(this, base.Handle),
					xDelta, yDelta,
					ref scroll, ref clip, hrgnUpdate, ref update, 2);
			}
			drawContext.ScrollPos = new Point(GetScrollInfo(Native.SB.HORZ).nPos, GetScrollInfo(Native.SB.VERT).nPos);
		}

		void SetScrollSize(Size sz, bool vRedraw, bool hRedraw)
		{
			scrollBarsInfo.scrollSize = sz;
			UpdateScrollBars(vRedraw, hRedraw);
		}

		void UpdateScrollBars(bool vRedraw, bool hRedraw)
		{
			InternalUpdateScrollBars(vRedraw, hRedraw, false);
		}

		void InternalUpdateScrollBars(bool vRedraw, bool hRedraw, bool redrawNow)
		{
			if (this.IsHandleCreated && Visible)
			{
				HandleRef handle = new HandleRef(this, this.Handle);

				Native.SCROLLINFO v = new Native.SCROLLINFO();
				v.cbSize = Marshal.SizeOf(typeof(Native.SCROLLINFO));
				v.fMask = Native.SIF.ALL;
				v.nMin = 0;
				v.nMax = scrollBarsInfo.scrollSize.Height;
				v.nPage = drawContext.ClientRect.Height;
				v.nPos = scrollBarsInfo.scrollPos.Y;
				v.nTrackPos = 0;
				Native.SetScrollInfo(handle, Native.SB.VERT, ref v, redrawNow && vRedraw);

				Native.SCROLLINFO h = new Native.SCROLLINFO();
				h.cbSize = Marshal.SizeOf(typeof(Native.SCROLLINFO));
				h.fMask = Native.SIF.ALL;
				h.nMin = 0;
				h.nMax = scrollBarsInfo.scrollSize.Width;
				h.nPage = drawContext.ClientRect.Width;
				h.nPos = scrollBarsInfo.scrollPos.X;
				h.nTrackPos = 0;
				Native.SetScrollInfo(handle, Native.SB.HORZ, ref h, redrawNow && hRedraw);

				if (!redrawNow)
				{
					scrollBarsInfo.vRedraw |= vRedraw;
					scrollBarsInfo.hRedraw |= hRedraw;
					if (!scrollBarsInfo.repaintPosted)
					{
						Native.PostMessage(handle, ScrollBarsInfo.WM_REPAINTSCROLLBARS, IntPtr.Zero, IntPtr.Zero);
						scrollBarsInfo.repaintPosted = true;
					}
				}
			}
		}

		void WMRepaintScrollBars()
		{
			InternalUpdateScrollBars(scrollBarsInfo.vRedraw, scrollBarsInfo.hRedraw, true);
			scrollBarsInfo.repaintPosted = false;
			scrollBarsInfo.hRedraw = false;
			scrollBarsInfo.vRedraw = false;
		}

		void WmHScroll(ref System.Windows.Forms.Message m)
		{
			int ret = DoWmScroll(ref m, scrollBarsInfo.scrollPos.X, scrollBarsInfo.scrollSize.Width, Native.SB.HORZ);
			if (ret >= 0)
			{
				this.SetScrollPos(new Point(ret, scrollBarsInfo.scrollPos.Y));
			}
			presenter.InvalidateTextLineUnderCursor();
		}

		Native.SCROLLINFO GetScrollInfo(Native.SB sb)
		{
			Native.SCROLLINFO si = new Native.SCROLLINFO();
			si.cbSize = Marshal.SizeOf(typeof(Native.SCROLLINFO));
			si.fMask = Native.SIF.ALL;
			Native.GetScrollInfo(new HandleRef(this, base.Handle), sb, ref si);
			return si;
		}

		int DoWmScroll(ref System.Windows.Forms.Message m,
			int num, int maximum, Native.SB bar)
		{
			if (m.LParam != IntPtr.Zero)
			{
				base.WndProc(ref m);
				return -1;
			}
			else
			{
				int smallChange = 50;
				int largeChange = 200;

				Native.SB sbEvt = (Native.SB)Native.LOWORD(m.WParam);
				switch (sbEvt)
				{
					case Native.SB.LINEUP:
						num -= smallChange;
						if (num <= 0)
							num = 0;
						break;

					case Native.SB.LINEDOWN:
						num += smallChange;
						if (num >= maximum)
							num = maximum;
						break;

					case Native.SB.PAGEUP:
						num -= largeChange;
						if (num <= 0)
							num = 0;
						break;

					case Native.SB.PAGEDOWN:
						num += largeChange;
						if (num >= maximum)
							num = maximum;
						break;

					case Native.SB.THUMBTRACK:
						scrollBarsInfo.userIsScrolling = true;
						num = this.GetScrollInfo(bar).nTrackPos;
						break;

					case Native.SB.THUMBPOSITION:
						num = this.GetScrollInfo(bar).nTrackPos;
						break;

					case Native.SB.TOP:
						num = 0;
						break;

					case Native.SB.BOTTOM:
						num = maximum;
						break;

					case Native.SB.ENDSCROLL:
						scrollBarsInfo.userIsScrolling = false;
						break;
				}

				return num;
			}
		}

		void WmVScroll(ref System.Windows.Forms.Message m)
		{
			int ret = DoWmScroll(ref m, scrollBarsInfo.scrollPos.Y, scrollBarsInfo.scrollSize.Height, Native.SB.VERT);
			if (ret >= 0)
			{
				this.SetScrollPos(new Point(scrollBarsInfo.scrollPos.X, ret));
			}
		}

		int GetDisplayMessagesCount()
		{
			return presenter != null ? presenter.DisplayMessages.Count : 0;
		}

		static class Native
		{
			[StructLayout(LayoutKind.Sequential)]
			public struct SCROLLINFO
			{
				public int cbSize;
				public SIF fMask;
				public int nMin;
				public int nMax;
				public int nPage;
				public int nPos;
				public int nTrackPos;
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct RECT
			{
				public int left;
				public int top;
				public int right;
				public int bottom;
				public RECT(Rectangle r)
				{
					left = r.Left;
					top = r.Top;
					right = r.Right;
					bottom = r.Bottom;
				}
				public Rectangle ToRectangle()
				{
					return new Rectangle(left, top, right - left, bottom - top);
				}
			}

			[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
			public static extern int SetScrollInfo(HandleRef hWnd, SB fnBar, ref SCROLLINFO si, bool redraw);

			[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
			public static extern bool GetScrollInfo(HandleRef hWnd, SB fnBar, ref SCROLLINFO si);

			[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
			public static extern int ScrollWindowEx(
				HandleRef hWnd,
				int nXAmount, int nYAmount,
				ref RECT rectScrollRegion,
				ref RECT rectClip,
				HandleRef hrgnUpdate,
				ref RECT prcUpdate,
				int flags);

			[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
			public static extern int RedrawWindow(
				HandleRef hWnd,
				IntPtr rectClip,
				IntPtr hrgnUpdate,
				UInt32 flags
			);

			[DllImport("user32.dll", CharSet = CharSet.Auto)]
			public static extern bool PostMessage(HandleRef hwnd,
				int msg, IntPtr wparam, IntPtr lparam);

			public const int WM_USER = 0x0400;

			public static int LOWORD(int n)
			{
				return (n & 0xffff);
			}

			public static int LOWORD(IntPtr n)
			{
				return LOWORD((int)((long)n));
			}

			public enum SB : int
			{
				LINEUP = 0,
				LINELEFT = 0,
				LINEDOWN = 1,
				LINERIGHT = 1,
				PAGEUP = 2,
				PAGELEFT = 2,
				PAGEDOWN = 3,
				PAGERIGHT = 3,
				THUMBPOSITION = 4,
				THUMBTRACK = 5,
				TOP = 6,
				LEFT = 6,
				BOTTOM = 7,
				RIGHT = 7,
				ENDSCROLL = 8,

				HORZ = 0,
				VERT = 1,
				BOTH = 3,
			}

			public enum SIF : uint
			{
				RANGE = 0x0001,
				PAGE = 0x0002,
				POS = 0x0004,
				DISABLENOSCROLL = 0x0008,
				TRACKPOS = 0x0010,
				ALL = (RANGE | PAGE | POS | TRACKPOS),
			}
		};

		#endregion

		#region Data members

		Presenter presenter;
		
		LJTraceSource tracer = LJTraceSource.EmptyTracer;

		struct ScrollBarsInfo
		{
			public const int WM_REPAINTSCROLLBARS = Native.WM_USER + 98;
			public Point scrollPos;
			public Size scrollSize;
			public bool vRedraw;
			public bool hRedraw;
			public bool repaintPosted;
			public bool userIsScrolling;
		};
		ScrollBarsInfo scrollBarsInfo;

		class PresenterUpdate
		{
			public IMessage FocusedBeforeUpdate;
			public int RelativeForcusedScrollPositionBeforeUpdate;
		};
		PresenterUpdate presenterUpdate;

		DrawContext drawContext = new DrawContext();
		BufferedGraphicsContext bufferedGraphicsContext;
		SelectionInfo selection { get { return presenter != null ? presenter.Selection : new SelectionInfo(); } }
		EverythingFilteredOutMessage everythingFilteredOutMessage;
		EmptyMessagesCollectionMessage emptyMessagesCollectionMessage;
		Lazy<FontFamily> fontFamily = new Lazy<FontFamily>(InitializeFontFamily);

		#endregion
	}
}
