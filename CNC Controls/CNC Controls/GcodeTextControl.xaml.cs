using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;

namespace CNC.Controls
{
    public enum GCodeLineStatus
    {
        Ready,
        Complete,
        Error,
        Info
    }

    /// <summary>
    /// Interaction logic for GcodeTextControl.xaml
    /// </summary>
    public partial class GcodeTextControl : UserControl
    {
        private readonly RangeLineNumberMargin statusMargin = new RangeLineNumberMargin();

        public GcodeTextControl()
        {
            InitializeComponent();

            using (var stream = GetType().Assembly.GetManifestResourceStream("CNC.Controls.Resources.gcode.xshd"))
            using (var reader = XmlReader.Create(stream))
                Editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);

            Editor.TextArea.LeftMargins.Insert(0, statusMargin);
        }

        public void SetRange(int startLine, int endLine)
        {
            statusMargin.SetRange(startLine, endLine, GCodeLineStatus.Ready);
        }

        public void SetRange(int startLine, int endLine, GCodeLineStatus status)
        {
            statusMargin.SetRange(startLine, endLine, status);
        }

        public void AddRange(int startLine, int endLine, GCodeLineStatus status)
        {
            statusMargin.AddRange(startLine, endLine, status);
        }

        public void ClearRange()
        {
            statusMargin.ClearRange();
        }
    }

    class RangeLineNumberMargin : AbstractMargin
    {
        private struct StatusRange
        {
            public int StartLine { get; set; }
            public int EndLine { get; set; }
            public GCodeLineStatus Status { get; set; }
        }

        private static readonly Typeface Typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Brush DefaultBrush = Brushes.White;
        private readonly List<StatusRange> ranges = new List<StatusRange>();

        public RangeLineNumberMargin()
        {
            Width = 48d;
        }

        public void SetRange(int startLine, int endLine, GCodeLineStatus status)
        {
            ranges.Clear();

            if (startLine <= 0 || endLine <= 0)
            {
                InvalidateVisual();
                return;
            }

            AddRange(startLine, endLine, status);
        }

        public void AddRange(int startLine, int endLine, GCodeLineStatus status)
        {
            if (startLine <= 0 || endLine <= 0)
                return;

            ranges.Add(new StatusRange
            {
                StartLine = Math.Min(startLine, endLine),
                EndLine = Math.Max(startLine, endLine),
                Status = status
            });

            InvalidateVisual();
        }

        public void ClearRange()
        {
            ranges.Clear();
            InvalidateVisual();
        }

        private Brush GetBrush(int lineNumber)
        {
            for (int i = ranges.Count - 1; i >= 0; i--)
            {
                if (lineNumber >= ranges[i].StartLine && lineNumber <= ranges[i].EndLine)
                {
                    switch (ranges[i].Status)
                    {
                        case GCodeLineStatus.Ready:
                            return Brushes.Gray;
                        case GCodeLineStatus.Complete:
                            return Brushes.LimeGreen;
                        case GCodeLineStatus.Error:
                            return Brushes.Red;
                        case GCodeLineStatus.Info:
                            return Brushes.Yellow;
                    }
                }
            }

            return DefaultBrush;
        }

        private void UpdateWidth()
        {
            int digits = Math.Max(2, ((TextView?.Document?.LineCount) ?? 1).ToString(CultureInfo.InvariantCulture).Length);
            double width = 16d + (digits * 8d);

            if (!width.Equals(Width))
                Width = width;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (TextView == null || !TextView.VisualLinesValid)
                return;

            UpdateWidth();

            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            foreach (var visualLine in TextView.VisualLines)
            {
                int lineNumber = visualLine.FirstDocumentLine.LineNumber;

                if (visualLine.TextLines.Count == 0)
                    continue;

                var marker = new FormattedText(
                    lineNumber.ToString(CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    Typeface,
                    14d,
                    GetBrush(lineNumber),
                    pixelsPerDip);

                double x = Math.Max(2d, ActualWidth - marker.WidthIncludingTrailingWhitespace - 4d);
                double y = visualLine.GetTextLineVisualYPosition(visualLine.TextLines[0], VisualYPosition.TextMiddle) - TextView.VerticalOffset - (marker.Height / 2d);

                drawingContext.DrawText(marker, new Point(x, Math.Max(0d, y)));
            }
        }
    }
}
