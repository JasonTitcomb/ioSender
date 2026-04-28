using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using System.ComponentModel;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using CNC.Core;

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
        private GrblViewModel model;

        public GcodeTextControl()
        {
            InitializeComponent();

            using (var stream = GetType().Assembly.GetManifestResourceStream("CNC.Controls.Resources.gcode.xshd"))
            using (var reader = XmlReader.Create(stream))
                Editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);

            Editor.TextArea.LeftMargins.Insert(0, statusMargin);
            Editor.TextArea.SelectionChanged += Editor_SelectionChanged;
            GCode.File.GetEditedText = () => Editor.Text;
            ctxMenu.DataContext = this;
        }

        #region Dependency properties

        public static readonly DependencyProperty SingleSelectedProperty = DependencyProperty.Register(nameof(SingleSelected), typeof(bool), typeof(GcodeTextControl), new PropertyMetadata(false));
        public bool SingleSelected
        {
            get { return (bool)GetValue(SingleSelectedProperty); }
            private set { SetValue(SingleSelectedProperty, value); }
        }

        public static readonly DependencyProperty MultipleSelectedProperty = DependencyProperty.Register(nameof(MultipleSelected), typeof(bool), typeof(GcodeTextControl), new PropertyMetadata(false));
        public bool MultipleSelected
        {
            get { return (bool)GetValue(MultipleSelectedProperty); }
            private set { SetValue(MultipleSelectedProperty, value); }
        }

        #endregion

        //add AllowEditing property to enable/disable editing of the text
        public bool IsReadonly
        {
            get => Editor.IsReadOnly == false;
            set => Editor.IsReadOnly = !value;
        }

        public void LoadFile(string filename)
        {
            if (!string.IsNullOrEmpty(filename) && File.Exists(filename))
                Editor.Load(filename);
            else
                Editor.Clear();

            Editor.IsModified = false;
        }

        public bool SaveAndReload(string filename = null)
        {
            filename = string.IsNullOrEmpty(filename) ? GCode.File.FileName : filename;

            if (string.IsNullOrEmpty(filename))
                return false;

            Editor.Save(filename);
            GCode.File.LoadFromEditor(filename, Editor.Text);
            Editor.IsModified = false;

            return true;
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

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is GrblViewModel newModel)
            {
                if (model != null)
                    model.PropertyChanged -= GcodeTextControl_PropertyChanged;

                model = newModel;
                model.PropertyChanged += GcodeTextControl_PropertyChanged;
            }
        }

        private void GcodeTextControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is GrblViewModel) switch (e.PropertyName)
            {
                case nameof(GrblViewModel.ScrollPosition):
                    int sp = ((GrblViewModel)sender).ScrollPosition;
                    if (sp <= 0)
                        Editor.ScrollToHome();
                    else
                        Editor.ScrollTo(sp + 1, 1);
                    break;
            }
        }

        private void Editor_Drag(object sender, DragEventArgs e)
        {
            GCode.File.Drag(sender, e);
        }

        private void Editor_Drop(object sender, DragEventArgs e)
        {
            GCode.File.Drop(sender, e);
        }

        private void Editor_SelectionChanged(object sender, EventArgs e)
        {
            int startLine, endLine;
            int selected = GetSelectedLineCount(out startLine, out endLine);

            bool canStart = DataContext is GrblViewModel && (DataContext as GrblViewModel).StartFromBlock.CanExecute(Math.Max(0, startLine - 1));

            SingleSelected = selected == 1 && canStart;
            MultipleSelected = selected >= 1 && canStart;
        }

        private int GetSelectedLineCount(out int startLine, out int endLine)
        {
            startLine = endLine = 0;

            if (Editor.Document == null || Editor.TextArea.Selection == null || Editor.TextArea.Selection.IsEmpty)
                return 0;

            var selection = Editor.TextArea.Selection.SurroundingSegment;
            if (selection == null)
                return 0;

            startLine = Editor.Document.GetLineByOffset(selection.Offset).LineNumber;
            int endOffset = Math.Max(selection.Offset, selection.EndOffset - 1);
            endLine = Editor.Document.GetLineByOffset(endOffset).LineNumber;

            return (endLine - startLine) + 1;
        }

        private string GetLineText(int lineNumber)
        {
            if (Editor.Document == null || lineNumber <= 0 || lineNumber > Editor.Document.LineCount)
                return string.Empty;

            return Editor.Document.GetText(Editor.Document.GetLineByNumber(lineNumber));
        }

        private List<string> GetSelectedLines()
        {
            var lines = new List<string>();
            int startLine, endLine;

            if (GetSelectedLineCount(out startLine, out endLine) == 0)
                return lines;

            for (int line = startLine; line <= endLine; line++)
            {
                string text = GetLineText(line);
                if (!string.IsNullOrWhiteSpace(text))
                    lines.Add(text);
            }

            return lines;
        }

        private void StartHere_Click(object sender, RoutedEventArgs e)
        {
            int startLine, endLine;

            if (GetSelectedLineCount(out startLine, out endLine) == 1 &&
                 ShowThemedMessageBox(string.Format(LibStrings.FindResource("VerifyStartFrom"), startLine),
                                      "ioSender", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                (DataContext as GrblViewModel).StartFromBlock.Execute(Math.Max(0, startLine - 1));
            }
        }

        private void CopyMDI_Click(object sender, RoutedEventArgs e)
        {
            int startLine, endLine;

            if (GetSelectedLineCount(out startLine, out endLine) == 1)
                (DataContext as GrblViewModel).MDIText = GetLineText(startLine);
        }

        private void SendController_Click(object sender, RoutedEventArgs e)
        {
            var lines = GetSelectedLines();

            if (lines.Count >= 1 &&
                 ShowThemedMessageBox(LibStrings.FindResource("VerifySendController"), "ioSender",
                                      MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                var vm = DataContext as GrblViewModel;

                if (vm.GrblError != 0)
                    vm.ExecuteCommand("");

                foreach (var line in lines)
                    vm.ExecuteCommand(line);
            }
        }

        private MessageBoxResult ShowThemedMessageBox(string message, string caption, MessageBoxButton buttons, MessageBoxImage icon, MessageBoxResult defaultResult)
        {
            var owner = Window.GetWindow(this) ?? Application.Current?.MainWindow;
            var dialog = new Window
            {
                Title = caption,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
                SizeToContent = SizeToContent.WidthAndHeight,
                MinWidth = 360,
                MaxWidth = 640,
                Content = BuildDialogContent(message, buttons, defaultResult, icon)
            };

            dialog.ShowDialog();
            return dialog.Tag is MessageBoxResult ? (MessageBoxResult)dialog.Tag : defaultResult;
        }

        private UIElement BuildDialogContent(string message, MessageBoxButton buttons, MessageBoxResult defaultResult, MessageBoxImage icon)
        {
            var root = new DockPanel { Margin = new Thickness(16) };

            var textPanel = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(textPanel, Dock.Top);

            var iconBlock = new TextBlock
            {
                Text = icon == MessageBoxImage.Question ? "?" : icon == MessageBoxImage.Warning ? "!" : "i",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Top
            };

            var messageBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                MinWidth = 280,
                MaxWidth = 580
            };

            textPanel.Children.Add(iconBlock);
            textPanel.Children.Add(messageBlock);
            root.Children.Add(textPanel);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            DockPanel.SetDock(buttonPanel, Dock.Bottom);

            AddDialogButtons(buttonPanel, buttons, defaultResult);
            root.Children.Add(buttonPanel);

            return root;
        }

        private void AddDialogButtons(StackPanel panel, MessageBoxButton buttons, MessageBoxResult defaultResult)
        {
            switch (buttons)
            {
                case MessageBoxButton.OK:
                    panel.Children.Add(CreateDialogButton("OK", MessageBoxResult.OK, defaultResult));
                    break;

                case MessageBoxButton.OKCancel:
                    panel.Children.Add(CreateDialogButton("OK", MessageBoxResult.OK, defaultResult));
                    panel.Children.Add(CreateDialogButton("Cancel", MessageBoxResult.Cancel, defaultResult));
                    break;

                case MessageBoxButton.YesNo:
                    panel.Children.Add(CreateDialogButton("Yes", MessageBoxResult.Yes, defaultResult));
                    panel.Children.Add(CreateDialogButton("No", MessageBoxResult.No, defaultResult));
                    break;

                default:
                    panel.Children.Add(CreateDialogButton("Yes", MessageBoxResult.Yes, defaultResult));
                    panel.Children.Add(CreateDialogButton("No", MessageBoxResult.No, defaultResult));
                    panel.Children.Add(CreateDialogButton("Cancel", MessageBoxResult.Cancel, defaultResult));
                    break;
            }
        }

        private Button CreateDialogButton(string caption, MessageBoxResult result, MessageBoxResult defaultResult)
        {
            var button = new Button
            {
                Content = caption,
                MinWidth = 85,
                Margin = new Thickness(6, 0, 0, 0),
                IsDefault = result == defaultResult,
                IsCancel = result == MessageBoxResult.Cancel || result == MessageBoxResult.No
            };

            button.Click += (s, e) =>
            {
                var win = Window.GetWindow((Button)s);
                if (win != null)
                {
                    win.Tag = result;
                    win.DialogResult = true;
                    win.Close();
                }
            };

            return button;
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

        private static readonly Brush BackgroundBrush = new SolidColorBrush(Color.FromRgb(40, 40, 40));
        private static readonly Pen DividerPen = new Pen(new SolidColorBrush(Color.FromRgb(80, 80, 80)), 1d);
        private readonly List<StatusRange> ranges = new List<StatusRange>();

        static RangeLineNumberMargin()
        {
            BackgroundBrush.Freeze();
            DividerPen.Brush.Freeze();
            DividerPen.Freeze();
        }

        public RangeLineNumberMargin()
        {
            Width = 18d;
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

            return null;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            drawingContext.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, RenderSize.Width, RenderSize.Height));
            drawingContext.DrawLine(DividerPen, new Point(RenderSize.Width - 0.5d, 0), new Point(RenderSize.Width - 0.5d, RenderSize.Height));

            if (TextView == null || !TextView.VisualLinesValid)
                return;

            foreach (var visualLine in TextView.VisualLines)
            {
                int lineNumber = visualLine.FirstDocumentLine.LineNumber;

                if (visualLine.TextLines.Count == 0)
                    continue;

                Brush brush = GetBrush(lineNumber);
                if (brush == null)
                    continue;

                double y = visualLine.GetTextLineVisualYPosition(visualLine.TextLines[0], VisualYPosition.TextMiddle) - TextView.VerticalOffset;
                drawingContext.DrawEllipse(brush, null, new Point(RenderSize.Width / 2d, Math.Max(0d, y)), 4d, 4d);
            }
        }
    }
}
