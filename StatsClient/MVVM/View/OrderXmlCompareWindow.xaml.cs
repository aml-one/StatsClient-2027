using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace StatsClient.MVVM.View;

public partial class OrderXmlCompareWindow : Window
{
    private readonly string _xmlFilePath;
    private readonly string _stCopyFilePath;
    private ScrollViewer? _xmlScrollViewer;
    private ScrollViewer? _stCopyScrollViewer;
    private bool _isSyncingScroll;
    private bool _isBuilding;
    private string _xmlText = string.Empty;
    private string _stCopyText = string.Empty;
    private bool _hasStCopy;

    private static readonly SolidColorBrush ForegroundDefault = Brush("#FFD4D4D4");
    private static readonly SolidColorBrush ForegroundLineNo = Brush("#FF6A9955");
    private static readonly SolidColorBrush ForegroundMarkerAdded = Brush("#FF89D185");
    private static readonly SolidColorBrush ForegroundMarkerRemoved = Brush("#FFF48771");
    private static readonly SolidColorBrush ForegroundTag = Brush("#FF569CD6");
    private static readonly SolidColorBrush ForegroundAttr = Brush("#FF9CDCFE");
    private static readonly SolidColorBrush ForegroundValue = Brush("#FFCE9178");
    private static readonly SolidColorBrush ForegroundComment = Brush("#FF6A9955");
    private static readonly SolidColorBrush BgRemoved = Brush("#FF44252B");
    private static readonly SolidColorBrush BgAdded = Brush("#FF1F4A36");
    private static readonly SolidColorBrush BgSpacer = Brush("#FF26282B");

    public OrderXmlCompareWindow(string xmlFilePath, string stCopyFilePath)
    {
        InitializeComponent();

        _xmlFilePath = xmlFilePath;
        _stCopyFilePath = stCopyFilePath;

        Loaded += OrderXmlCompareWindow_Loaded;
    }

    private async void OrderXmlCompareWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _xmlText = SafeReadAllText(_xmlFilePath);
        _hasStCopy = File.Exists(_stCopyFilePath);
        _stCopyText = _hasStCopy ? SafeReadAllText(_stCopyFilePath) : string.Empty;

        tbXmlTitle.Text = _hasStCopy ? Path.GetFileName(_stCopyFilePath) : Path.GetFileName(_xmlFilePath);
        tbStCopyTitle.Text = Path.GetFileName(_xmlFilePath);

        await BuildEditorsAsync(_xmlText, _stCopyText, _hasStCopy);

        _xmlScrollViewer = FindScrollViewer(editorXml);
        _stCopyScrollViewer = FindScrollViewer(editorStCopy);

        if (_xmlScrollViewer is not null)
        {
            _xmlScrollViewer.ScrollChanged += XmlScrollViewer_ScrollChanged;
        }

        if (_stCopyScrollViewer is not null)
        {
            _stCopyScrollViewer.ScrollChanged += StCopyScrollViewer_ScrollChanged;
        }
    }

    private static string SafeReadAllText(string filePath)
    {
        try
        {
            return File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task BuildEditorsAsync(string xmlText, string stCopyText, bool hasStCopy)
    {
        if (_isBuilding)
        {
            return;
        }

        _isBuilding = true;
        try
        {
            ShowLoading(0, "Loading files...");
            await Task.Yield();

            string[] xmlLines = SplitLines(xmlText);
            bool ignoreTrailingSpaces = checkIgnoreTrailingSpaces.IsChecked == true;

            if (!hasStCopy)
            {
                editorXml.Document = await BuildSingleDocumentAsync(xmlLines);
                editorStCopy.Document = BuildInfoDocument("No <orderID>.stCopy file exists for this order.");
                tbStatus.Text = "Read-only view";
                tbChangeCount.Text = "0 changes";
                tbRemovedCount.Text = "-0";
                tbAddedCount.Text = "+0";
                HideLoading();
                return;
            }

            string[] stCopyLines = SplitLines(stCopyText);

            ShowLoading(18, "Preparing diff map...");
            IReadOnlyList<DiffRow> rows = await Task.Run(() => BuildAlignedRows(stCopyLines, xmlLines, ignoreTrailingSpaces));

            int added = rows.Count(x => x.RightKind == DiffKind.Added);
            int removed = rows.Count(x => x.LeftKind == DiffKind.Removed);
            int changed = added + removed;
            tbStatus.Text = "Read-only compare view";
            tbChangeCount.Text = $"{changed} changes";
            tbRemovedCount.Text = $"-{removed}";
            tbAddedCount.Text = $"+{added}";

            ShowLoading(45, "Rendering original (stCopy)...");
            editorXml.Document = await BuildDiffDocumentAsync(rows, leftSide: true, progressStart: 45, progressEnd: 70);

            ShowLoading(72, "Rendering new (XML)...");
            editorStCopy.Document = await BuildDiffDocumentAsync(rows, leftSide: false, progressStart: 72, progressEnd: 98);

            HideLoading();
        }
        finally
        {
            _isBuilding = false;
        }
    }

    private async Task<FlowDocument> BuildSingleDocumentAsync(IReadOnlyList<string> lines)
    {
        var rows = new List<DiffRow>(lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            rows.Add(new DiffRow(lines[i], i + 1, DiffKind.Unchanged, null, null, DiffKind.Unchanged));
        }

        return await BuildDiffDocumentAsync(rows, leftSide: true, progressStart: 20, progressEnd: 95);
    }

    private static string[] SplitLines(string text)
        => text.Replace("\r\n", "\n").Split('\n');

    private static FlowDocument BuildInfoDocument(string message)
    {
        FlowDocument document = CreateBaseDocument();
        Paragraph paragraph = new(new Run(message))
        {
            Margin = new Thickness(0),
            Foreground = ForegroundDefault
        };
        document.Blocks.Add(paragraph);
        return document;
    }

    private static IReadOnlyList<DiffRow> BuildAlignedRows(IReadOnlyList<string> leftLines, IReadOnlyList<string> rightLines, bool ignoreTrailingSpaces)
    {
        const int lookAhead = 30;
        List<DiffRow> rows = [];

        string[] leftCompare = leftLines.Select(x => NormalizeForCompare(x, ignoreTrailingSpaces)).ToArray();
        string[] rightCompare = rightLines.Select(x => NormalizeForCompare(x, ignoreTrailingSpaces)).ToArray();

        int i = 0;
        int j = 0;

        while (i < leftLines.Count && j < rightLines.Count)
        {
            if (string.Equals(leftCompare[i], rightCompare[j], StringComparison.Ordinal))
            {
                rows.Add(new DiffRow(leftLines[i], i + 1, DiffKind.Unchanged, rightLines[j], j + 1, DiffKind.Unchanged));
                i++;
                j++;
                continue;
            }

            int leftMatch = FindMatch(leftCompare, i + 1, Math.Min(leftCompare.Length - 1, i + lookAhead), rightCompare[j]);
            int rightMatch = FindMatch(rightCompare, j + 1, Math.Min(rightCompare.Length - 1, j + lookAhead), leftCompare[i]);

            if (leftMatch >= 0 && (rightMatch < 0 || (leftMatch - i) <= (rightMatch - j)))
            {
                for (int k = i; k < leftMatch; k++)
                {
                    rows.Add(new DiffRow(leftLines[k], k + 1, DiffKind.Removed, null, null, DiffKind.Spacer));
                }

                rows.Add(new DiffRow(leftLines[leftMatch], leftMatch + 1, DiffKind.Unchanged, rightLines[j], j + 1, DiffKind.Unchanged));
                i = leftMatch + 1;
                j++;
                continue;
            }

            if (rightMatch >= 0)
            {
                for (int k = j; k < rightMatch; k++)
                {
                    rows.Add(new DiffRow(null, null, DiffKind.Spacer, rightLines[k], k + 1, DiffKind.Added));
                }

                rows.Add(new DiffRow(leftLines[i], i + 1, DiffKind.Unchanged, rightLines[rightMatch], rightMatch + 1, DiffKind.Unchanged));
                i++;
                j = rightMatch + 1;
                continue;
            }

            rows.Add(new DiffRow(leftLines[i], i + 1, DiffKind.Removed, rightLines[j], j + 1, DiffKind.Added));
            i++;
            j++;
        }

        while (i < leftLines.Count)
        {
            rows.Add(new DiffRow(leftLines[i], i + 1, DiffKind.Removed, null, null, DiffKind.Spacer));
            i++;
        }

        while (j < rightLines.Count)
        {
            rows.Add(new DiffRow(null, null, DiffKind.Spacer, rightLines[j], j + 1, DiffKind.Added));
            j++;
        }

        return rows;
    }

    private static int FindMatch(IReadOnlyList<string> lines, int start, int end, string value)
    {
        for (int index = start; index <= end; index++)
        {
            if (string.Equals(lines[index], value, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static string NormalizeForCompare(string line, bool ignoreTrailingSpaces)
    {
        if (!ignoreTrailingSpaces)
        {
            return line;
        }

        string normalized = line.TrimEnd();
        normalized = Regex.Replace(normalized, "\\s+/>", "/>");
        return normalized;
    }

    private void IgnoreTrailingSpaces_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _isBuilding)
        {
            return;
        }

        _ = BuildEditorsAsync(_xmlText, _stCopyText, _hasStCopy);
    }

    private async Task<FlowDocument> BuildDiffDocumentAsync(IReadOnlyList<DiffRow> rows, bool leftSide, double progressStart, double progressEnd)
    {
        FlowDocument document = CreateBaseDocument();

        int maxLineNumber = leftSide
            ? rows.Where(x => x.LeftLineNumber.HasValue).Select(x => x.LeftLineNumber!.Value).DefaultIfEmpty(0).Max()
            : rows.Where(x => x.RightLineNumber.HasValue).Select(x => x.RightLineNumber!.Value).DefaultIfEmpty(0).Max();

        int lineDigits = Math.Max(4, maxLineNumber.ToString().Length);

        for (int index = 0; index < rows.Count; index++)
        {
            DiffRow row = rows[index];
            string? line = leftSide ? row.LeftLine : row.RightLine;
            int? lineNo = leftSide ? row.LeftLineNumber : row.RightLineNumber;
            DiffKind kind = leftSide ? row.LeftKind : row.RightKind;

            Paragraph paragraph = new()
            {
                Margin = new Thickness(0),
                LineHeight = 16
            };

            paragraph.Background = kind switch
            {
                DiffKind.Added => BgAdded,
                DiffKind.Removed => BgRemoved,
                DiffKind.Spacer => BgSpacer,
                _ => Brushes.Transparent
            };

            string lineNoText = lineNo.HasValue ? lineNo.Value.ToString().PadLeft(lineDigits) : new string(' ', lineDigits);
            string marker = kind switch
            {
                DiffKind.Added => "+",
                DiffKind.Removed => "-",
                _ => " "
            };

            paragraph.Inlines.Add(new Run($"{lineNoText} ") { Foreground = ForegroundLineNo });
            paragraph.Inlines.Add(new Run($"{marker} ")
            {
                Foreground = kind == DiffKind.Removed ? ForegroundMarkerRemoved : kind == DiffKind.Added ? ForegroundMarkerAdded : ForegroundLineNo,
                FontWeight = FontWeights.SemiBold
            });

            if (!string.IsNullOrEmpty(line))
            {
                AppendXmlSyntaxRuns(paragraph.Inlines, line);
            }

            document.Blocks.Add(paragraph);

            if (index % 220 == 0)
            {
                double t = rows.Count == 0 ? 1 : (double)index / rows.Count;
                ShowLoading(progressStart + ((progressEnd - progressStart) * t), leftSide ? "Rendering XML..." : "Rendering stCopy...");
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        ShowLoading(progressEnd, leftSide ? "Rendering XML..." : "Rendering stCopy...");
        return document;
    }

    private static FlowDocument CreateBaseDocument()
    {
        return new FlowDocument
        {
            PagePadding = new Thickness(8, 4, 8, 4),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            LineHeight = 16,
            Foreground = ForegroundDefault,
            Background = Brushes.Transparent,
            ColumnWidth = 200000,
            PageWidth = 200000,
            IsOptimalParagraphEnabled = false
        };
    }

    private static void AppendXmlSyntaxRuns(InlineCollection inlines, string line)
    {
        MatchCollection matches = Regex.Matches(line, "(<[^>]+>)");
        int position = 0;
        foreach (Match match in matches)
        {
            if (match.Index > position)
            {
                inlines.Add(new Run(line[position..match.Index])
                {
                    Foreground = ForegroundDefault
                });
            }

            AppendTagRuns(inlines, match.Value);
            position = match.Index + match.Length;
        }

        if (position < line.Length)
        {
            inlines.Add(new Run(line[position..])
            {
                Foreground = ForegroundDefault
            });
        }
    }

    private static void AppendTagRuns(InlineCollection inlines, string tag)
    {
        if (tag.StartsWith("<!--", StringComparison.Ordinal))
        {
            inlines.Add(new Run(tag)
            {
                Foreground = ForegroundComment
            });
            return;
        }

        inlines.Add(new Run("<") { Foreground = ForegroundTag });

        string content = tag.TrimStart('<').TrimEnd('>');
        bool hasClosingSlash = content.EndsWith('/');
        if (hasClosingSlash)
        {
            content = content[..^1];
        }

        string nameAndRest = content;
        if (nameAndRest.StartsWith("/", StringComparison.Ordinal))
        {
            inlines.Add(new Run("/") { Foreground = ForegroundTag });
            nameAndRest = nameAndRest[1..];
        }

        string tagName = nameAndRest.Split(' ', 2)[0];
        string remainder = nameAndRest.Length > tagName.Length ? nameAndRest[tagName.Length..] : string.Empty;

        inlines.Add(new Run(tagName)
        {
            Foreground = ForegroundTag,
            FontWeight = FontWeights.SemiBold
        });

        MatchCollection attrs = Regex.Matches(remainder, "(\\s+)([\\w:-]+)(=)(\"[^\"]*\"|'[^']*')");
        int pos = 0;
        foreach (Match attr in attrs)
        {
            if (attr.Index > pos)
            {
                inlines.Add(new Run(remainder[pos..attr.Index]) { Foreground = ForegroundTag });
            }

            inlines.Add(new Run(attr.Groups[1].Value) { Foreground = ForegroundTag });
            inlines.Add(new Run(attr.Groups[2].Value) { Foreground = ForegroundAttr });
            inlines.Add(new Run(attr.Groups[3].Value) { Foreground = ForegroundTag });
            inlines.Add(new Run(attr.Groups[4].Value) { Foreground = ForegroundValue });
            pos = attr.Index + attr.Length;
        }

        if (pos < remainder.Length)
        {
            inlines.Add(new Run(remainder[pos..]) { Foreground = ForegroundTag });
        }

        if (hasClosingSlash)
        {
            inlines.Add(new Run("/") { Foreground = ForegroundTag });
        }

        inlines.Add(new Run(">") { Foreground = ForegroundTag });
    }

    private static SolidColorBrush Brush(string colorHex)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFrom(colorHex)!;
    }

    private void XmlScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isSyncingScroll || checkSyncScroll.IsChecked != true || _stCopyScrollViewer is null)
        {
            return;
        }

        SyncScroll(e, _stCopyScrollViewer);
    }

    private void StCopyScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isSyncingScroll || checkSyncScroll.IsChecked != true || _xmlScrollViewer is null)
        {
            return;
        }

        SyncScroll(e, _xmlScrollViewer);
    }

    private void SyncScroll(ScrollChangedEventArgs e, ScrollViewer target)
    {
        _isSyncingScroll = true;
        try
        {
            double ratio = e.ExtentHeight > 0 ? e.VerticalOffset / e.ExtentHeight : 0;
            double targetOffset = ratio * target.ExtentHeight;
            target.ScrollToVerticalOffset(targetOffset);
            target.ScrollToHorizontalOffset(e.HorizontalOffset);
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        if (parent is ScrollViewer scrollViewer)
        {
            return scrollViewer;
        }

        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            ScrollViewer? result = FindScrollViewer(child);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private void Editor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not RichTextBox editor)
        {
            return;
        }

        var scrollViewer = FindScrollViewer(editor);
        if (scrollViewer is null)
        {
            return;
        }

        if (e.Delta > 0)
        {
            scrollViewer.LineUp();
        }
        else
        {
            scrollViewer.LineDown();
        }

        e.Handled = true;
    }

    private void ShowLoading(double percent, string state)
    {
        if (FindName("pbLoad") is ProgressBar progressBar)
        {
            progressBar.Visibility = Visibility.Visible;
            progressBar.Value = Math.Clamp(percent, 0, 100);
        }

        if (FindName("tbLoadState") is TextBlock loadText)
        {
            loadText.Visibility = Visibility.Visible;
            loadText.Text = $"{state} {(int)Math.Round(percent)}%";
        }
    }

    private void HideLoading()
    {
        if (FindName("pbLoad") is ProgressBar progressBar)
        {
            progressBar.Value = 100;
            progressBar.Visibility = Visibility.Collapsed;
        }

        if (FindName("tbLoadState") is TextBlock loadText)
        {
            loadText.Text = "Completed 100%";
            loadText.Visibility = Visibility.Collapsed;
        }
    }

    private void TitleBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (IsClickInsideCloseButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
        }
    }

    private bool IsClickInsideCloseButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (ReferenceEquals(source, btnClose))
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        if (e is MouseButtonEventArgs mouseArgs)
        {
            mouseArgs.Handled = true;
        }

        Close();
    }

    private enum DiffKind
    {
        Unchanged,
        Added,
        Removed,
        Spacer
    }

    private sealed class DiffRow(string? leftLine, int? leftLineNumber, DiffKind leftKind, string? rightLine, int? rightLineNumber, DiffKind rightKind)
    {
        public string? LeftLine { get; } = leftLine;
        public int? LeftLineNumber { get; } = leftLineNumber;
        public DiffKind LeftKind { get; } = leftKind;
        public string? RightLine { get; } = rightLine;
        public int? RightLineNumber { get; } = rightLineNumber;
        public DiffKind RightKind { get; } = rightKind;
    }
}
