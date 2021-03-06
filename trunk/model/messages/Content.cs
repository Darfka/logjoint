
namespace LogJoint
{
	public sealed class Content : MessageBase, IContent
	{
		public Content(long position, long endPosition, IThread t, MessageTimestamp time, StringSlice msg, SeverityFlag s, StringSlice rawText = new StringSlice())
			:
			base(position, endPosition, t, time, rawText)
		{
			this.message = msg;
			this.flags = MessageFlag.Content | (MessageFlag)s;
		}

		SeverityFlag IContent.Severity
		{
			get
			{
				return (SeverityFlag)(flags & MessageFlag.ContentTypeMask);
			}
		}

		IMessage IMessage.Clone()
		{
			IContent intf = this;
			return new Content(intf.Position, intf.EndPosition, intf.Thread, intf.Time, message, intf.Severity, this.DoGetRawText());
		}

		#region Protected overrides

		protected override void DoVisit(IMessageVisitor visitor)
		{
			visitor.Visit(this);
		}

		protected override StringSlice DoGetText()
		{
			return message;
		}

		protected override void DoReallocateTextBuffer(IStringSliceReallocator alloc)
		{
			base.DoReallocateTextBuffer(alloc);
			message = alloc.Reallocate(message);
		}

		protected override bool DoWrapTooLongText(int maxLineLen)
		{
			var baseResult = base.DoWrapTooLongText(maxLineLen);
			return WrapIfTooLong(ref message, maxLineLen) || baseResult; 
		}

		#endregion


		StringSlice message;
	};

}
