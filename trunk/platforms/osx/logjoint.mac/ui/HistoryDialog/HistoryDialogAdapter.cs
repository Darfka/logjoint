﻿
using System;
using System.Collections.Generic;
using System.Linq;
using MonoMac.Foundation;
using MonoMac.AppKit;
using LogJoint.UI.Presenters.HistoryDialog;
using MonoMac.ObjCRuntime;

namespace LogJoint.UI
{
	public partial class HistoryDialogAdapter : MonoMac.AppKit.NSWindowController, IView
	{
		#region Constructors

		// Called when created from unmanaged code
		public HistoryDialogAdapter(IntPtr handle)
			: base(handle)
		{
			Initialize();
		}
		
		// Called when created directly from a XIB file
		[Export("initWithCoder:")]
		public HistoryDialogAdapter(NSCoder coder)
			: base(coder)
		{
			Initialize();
		}
		
		// Call to load from the XIB/NIB file
		public HistoryDialogAdapter()
			: base("HistoryDialog")
		{
			Initialize();
		}
		
		// Shared initialization code
		void Initialize()
		{
			quickSearchTextBoxAdapter = new QuickSearchTextBoxAdapter();

		}

		#endregion
	

		public override void AwakeFromNib()
		{
			base.AwakeFromNib();

			//outlineView.Delegate = new Delegate();

			quickSearchTextBoxAdapter.View.MoveToPlaceholder(quickSearchTextBoxPlaceholder);
		}

		//strongly typed window accessor
		public new HistoryDialog Window
		{
			get
			{
				return (HistoryDialog)base.Window;
			}
		}

		void IView.SetEventsHandler(IViewEvents viewEvents)
		{
			this.viewEvents = viewEvents;
		}

		void IView.Update(ViewItem[] items)
		{
			WillChangeValue ("ItemModelArray");
			data.RemoveAllObjects();
			ItemModel lastContainer = null;
			foreach (var i in items)
			{
				var itemModel = new ItemModel(i);
				if (itemModel.IsLeaf)
				{
					if (lastContainer != null)
						lastContainer.Add(itemModel);
					else
						data.Add(itemModel);
				}
				else
				{
					data.Add(itemModel);
					lastContainer = itemModel;
				}
			}
			DidChangeValue ("ItemModelArray");

			var x = outlineView.DataSource;
			treeController.ArrangedObjects.PerformSelector(new Selector("childNodes"), null, 0);
		}

		void IView.AboutToShow()
		{
			Window.GetHashCode(); // force nib loading
		}

		void IView.Show()
		{
			NSApplication.SharedApplication.RunModalForWindow(Window);
		}

		void IView.Hide()
		{
			NSApplication.SharedApplication.AbortModal();
			this.Close();
		}

		void IView.PutInputFocusToItemsList()
		{
		}

		void IView.EnableOpenButton(bool enable)
		{
			openButton.Enabled = enable;
		}

		bool IView.ShowClearHistroConfirmationDialog(string message)
		{
			return false;
		}

		void IView.ShowOpeningFailurePopup(string message)
		{
		}

		LogJoint.UI.Presenters.QuickSearchTextBox.IView IView.QuickSearchTextBox
		{
			get
			{
				return quickSearchTextBoxAdapter;
			}
		}

		ViewItem[] IView.SelectedItems
		{
			get
			{
				return new ViewItem[0];
			}
			set
			{
			}
		}

		partial void OnCancelButtonClicked (NSObject sender)
		{
			NSApplication.SharedApplication.AbortModal ();
			this.Close();
		}

		partial void OnOpenButtonClicked (NSObject sender)
		{
			NSApplication.SharedApplication.StopModal ();
			this.Close();
		}

		[Export("ItemModelArray")]
		public NSArray Data 
		{
			get { return data; }
		}


		IViewEvents viewEvents;
		QuickSearchTextBoxAdapter quickSearchTextBoxAdapter;

		private NSMutableArray data = new NSMutableArray();

	}

	[Register("ItemModel")]
	public class ItemModel : NSObject
	{
		private ViewItem data;
		private NSMutableArray children = new NSMutableArray();

		[Export("Text")]
		public string Text {
			get { return data.Text; }
		}

		[Export("Annotation")]
		public string Annotation {
			get { return data.Annotation; }
		}

		[Export("ItemModelArray")]
		public NSArray Children {
			get { return children; }
		}

		[Export("NumberOfChildren")]
		public uint NumberOfChildren {
			get { return children.Count; }
		}

		[Export("IsLeaf")]
		public bool IsLeaf {
			get { return data.Type != ViewItemType.HistoryComment; }
		}

		[Export("IsSelectable")]
		public bool IsSelectable {
			get { return data.Type != ViewItemType.HistoryComment; }
		}

		[Export("Color")]
		public NSColor Color {
			get { return data.Type == ViewItemType.HistoryComment ? NSColor.Gray : NSColor.Black; }
		}


		public ItemModel(ViewItem item)
		{
			this.data = item;
		}

		public void Add(ItemModel i)
		{
			WillChangeValue ("ItemModelArray");
			children.Add(i);
			DidChangeValue ("ItemModelArray");
		}
	}
}

