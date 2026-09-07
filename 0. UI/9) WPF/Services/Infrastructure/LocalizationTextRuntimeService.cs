using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Automation;
using System.Windows.Threading;
using OpenVisionLab;

namespace MvcVisionSystem
{
    internal static class LocalizationTextRuntimeService
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<WeakReference<Window>> Windows = new List<WeakReference<Window>>();
        private static readonly List<WeakReference<FrameworkElement>> Roots = new List<WeakReference<FrameworkElement>>();
        private static readonly ConditionalWeakTable<DependencyObject, BindingRecordSet> BindingRecords =
            new ConditionalWeakTable<DependencyObject, BindingRecordSet>();
        private static readonly ConditionalWeakTable<DependencyObject, OriginalValueSet> OriginalValues =
            new ConditionalWeakTable<DependencyObject, OriginalValueSet>();
        private static readonly ConditionalWeakTable<DataGridColumn, OriginalColumnHeaderValue> OriginalColumnHeaders =
            new ConditionalWeakTable<DataGridColumn, OriginalColumnHeaderValue>();
        private static readonly ConditionalWeakTable<FrameworkElement, RootRefreshState> RootRefreshStates =
            new ConditionalWeakTable<FrameworkElement, RootRefreshState>();
        private static bool registered;
        private static OpenVisionLanguage cachedLanguage;
        private static IReadOnlyList<OpenVisionLocalizationEntry> cachedEntries = Array.Empty<OpenVisionLocalizationEntry>();
        private static bool hasCachedEntries;

        internal static void RegisterWindow(Window window)
        {
            if (window == null)
            {
                return;
            }

            EnsureRegistered();
            lock (SyncRoot)
            {
                for (int i = Windows.Count - 1; i >= 0; i--)
                {
                    if (!Windows[i].TryGetTarget(out Window registeredWindow))
                    {
                        Windows.RemoveAt(i);
                        continue;
                    }

                    if (ReferenceEquals(registeredWindow, window))
                    {
                        return;
                    }
                }

                Windows.Add(new WeakReference<Window>(window));
            }

            window.Closed += RegisteredWindow_Closed;

            if (window.IsLoaded)
            {
                RefreshWindow(window);
            }
        }

        internal static void RegisterRoot(FrameworkElement root)
        {
            if (root == null)
            {
                return;
            }

            EnsureRegistered();
            bool alreadyRegistered = false;
            lock (SyncRoot)
            {
                for (int i = Roots.Count - 1; i >= 0; i--)
                {
                    if (!Roots[i].TryGetTarget(out FrameworkElement registeredRoot))
                    {
                        Roots.RemoveAt(i);
                        continue;
                    }

                    if (ReferenceEquals(registeredRoot, root))
                    {
                        alreadyRegistered = true;
                        break;
                    }
                }

                if (!alreadyRegistered)
                {
                    Roots.Add(new WeakReference<FrameworkElement>(root));
                }
            }

            if (alreadyRegistered)
            {
                if (root.IsLoaded)
                {
                    RefreshRoot(root);
                }

                return;
            }

            root.Loaded += RegisteredRoot_Loaded;
            root.LayoutUpdated += RegisteredRoot_LayoutUpdated;
            if (root.IsLoaded)
            {
                RefreshRoot(root);
            }
        }

        internal static void RefreshAll()
        {
            List<Window> liveWindows = new List<Window>();
            List<FrameworkElement> liveRoots = new List<FrameworkElement>();
            lock (SyncRoot)
            {
                for (int i = Windows.Count - 1; i >= 0; i--)
                {
                    if (Windows[i].TryGetTarget(out Window window))
                    {
                        if (window.IsLoaded)
                        {
                            liveWindows.Add(window);
                        }
                    }
                    else
                    {
                        Windows.RemoveAt(i);
                    }
                }

                for (int i = Roots.Count - 1; i >= 0; i--)
                {
                    if (Roots[i].TryGetTarget(out FrameworkElement root))
                    {
                        liveRoots.Add(root);
                    }
                    else
                    {
                        Roots.RemoveAt(i);
                    }
                }
            }

            foreach (Window window in liveWindows)
            {
                if (window.Dispatcher.CheckAccess())
                {
                    window.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() => RefreshWindow(window)));
                }
                else
                {
                    window.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                    new Action(() => RefreshWindow(window)));
                }
            }

            foreach (FrameworkElement root in liveRoots)
            {
                if (!root.IsLoaded)
                {
                    continue;
                }

                RootRefreshStates.GetOrCreateValue(root).Pending = true;
                QueueRootRefresh(root);
            }
        }

        internal static string Translate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            IReadOnlyList<OpenVisionLocalizationEntry> entries = GetCachedEntries();
            if (OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.Korean)
            {
                foreach (OpenVisionLocalizationEntry entry in entries)
                {
                    if (string.Equals(value, entry.English, StringComparison.Ordinal))
                    {
                        return entry.Korean;
                    }

                    if (string.Equals(value.Trim(), entry.English.Trim(), StringComparison.Ordinal))
                    {
                        return entry.Korean;
                    }
                }

                return value;
            }

            foreach (OpenVisionLocalizationEntry entry in entries)
            {
                if (string.Equals(value, entry.Korean, StringComparison.Ordinal))
                {
                    return entry.English;
                }

                if (string.Equals(value.Trim(), entry.Korean.Trim(), StringComparison.Ordinal))
                {
                    return entry.English;
                }
            }

            // English presentation is owned by the catalog and the bound View/ViewModel
            // value. Do not manufacture sentences by replacing unordered Korean terms.
            return value;
        }

        private static void EnsureRegistered()
        {
            if (registered)
            {
                return;
            }

            registered = true;
            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(WindowLoaded));
            EventManager.RegisterClassHandler(
                typeof(TextBlock),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(TextBlockLoaded));
            OpenVisionLanguageService.LanguageChanged += OpenVisionLanguageService_LanguageChanged;
        }

        private static void WindowLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
            {
                RegisterWindow(window);
                window.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new Action(() => RefreshWindow(window)));
            }
        }

        private static void RegisteredWindow_Closed(object sender, EventArgs e)
        {
            if (!(sender is Window window))
            {
                return;
            }

            window.Closed -= RegisteredWindow_Closed;
            lock (SyncRoot)
            {
                for (int i = Windows.Count - 1; i >= 0; i--)
                {
                    if (!Windows[i].TryGetTarget(out Window registeredWindow)
                        || ReferenceEquals(registeredWindow, window))
                    {
                        Windows.RemoveAt(i);
                    }
                }
            }
        }

        private static void RegisteredRoot_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement root)
            {
                RootRefreshStates.GetOrCreateValue(root).Pending = true;
                QueueRootRefresh(root);
            }
        }

        private static void TextBlockLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                ApplyStaticText(textBlock, TextBlock.TextProperty, GetText, SetText);
                RefreshBoundText(textBlock, TextBlock.TextProperty);
            }
        }

        private static void RegisteredRoot_LayoutUpdated(object sender, EventArgs e)
        {
            if (!(sender is FrameworkElement root) || !root.IsLoaded)
            {
                return;
            }

            RootRefreshState state = RootRefreshStates.GetOrCreateValue(root);
            if (!state.Pending || state.Queued)
            {
                return;
            }

            state.Queued = true;
            root.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    state.Pending = false;
                    state.Queued = false;
                    RefreshRoot(root);
                    QueueSecondRootRefresh(root);
                }));
        }

        private static void QueueRootRefresh(FrameworkElement root)
        {
            root.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    if (!root.IsLoaded)
                    {
                        return;
                    }

                    RefreshRoot(root);
                    QueueSecondRootRefresh(root);
                }));
        }

        private static void QueueSecondRootRefresh(FrameworkElement root)
        {
            root.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    if (!root.IsLoaded)
                    {
                        return;
                    }

                    RefreshRoot(root);
                    ScheduleDelayedRootRefresh(root);
                }));
        }

        private static void ScheduleDelayedRootRefresh(FrameworkElement root)
        {
            if (root == null || !root.IsLoaded)
            {
                return;
            }

            RootRefreshState state = RootRefreshStates.GetOrCreateValue(root);
            StopDelayedRootRefresh(state);
            DispatcherTimer delayedRefresh = new DispatcherTimer(
                DispatcherPriority.Background,
                root.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            WeakReference<FrameworkElement> weakRoot = new WeakReference<FrameworkElement>(root);
            EventHandler delayedRefreshHandler = (sender, args) =>
            {
                StopDelayedRootRefresh(state);
                if (weakRoot.TryGetTarget(out FrameworkElement target) && target.IsLoaded)
                {
                    RefreshRoot(target);
                }
            };
            state.DelayedRefreshTimer = delayedRefresh;
            state.DelayedRefreshHandler = delayedRefreshHandler;
            delayedRefresh.Tick += delayedRefreshHandler;
            delayedRefresh.Start();
        }

        private static void StopDelayedRootRefresh(RootRefreshState state)
        {
            if (state?.DelayedRefreshTimer == null)
            {
                return;
            }

            state.DelayedRefreshTimer.Stop();
            if (state.DelayedRefreshHandler != null)
            {
                state.DelayedRefreshTimer.Tick -= state.DelayedRefreshHandler;
            }

            state.DelayedRefreshTimer = null;
            state.DelayedRefreshHandler = null;
        }

        private static void OpenVisionLanguageService_LanguageChanged(object sender, EventArgs e)
        {
            lock (SyncRoot)
            {
                hasCachedEntries = false;
            }
            RefreshAll();
        }

        private static IReadOnlyList<OpenVisionLocalizationEntry> GetCachedEntries()
        {
            OpenVisionLanguage language = OpenVisionLanguageService.CurrentLanguage;
            lock (SyncRoot)
            {
                if (!hasCachedEntries || cachedLanguage != language)
                {
                    cachedLanguage = language;
                    cachedEntries = OpenVisionLanguageService.GetEntries();
                    hasCachedEntries = true;
                }

                return cachedEntries;
            }
        }

        private static void RefreshWindow(Window window)
        {
            if (window == null || !window.IsLoaded)
            {
                return;
            }

            ApplyWindowTitle(window);
            RefreshRoot(window);
        }

        private static void RefreshRoot(DependencyObject root)
        {
            if (root == null)
            {
                return;
            }

            FrameworkElement frameworkElement = root as FrameworkElement;
            if (frameworkElement != null && !frameworkElement.IsLoaded)
            {
                return;
            }

            RefreshRootPass(root);
            if (frameworkElement != null && frameworkElement.IsLoaded)
            {
                frameworkElement.UpdateLayout();
                RefreshRootPass(root);
            }
        }

        private static void RefreshRootPass(DependencyObject root)
        {
            foreach (DependencyObject element in EnumerateVisualTree(root))
            {
                if (element is DataGrid dataGrid)
                {
                    ApplyDataGridColumnHeaders(dataGrid);
                }

                if (element is TextBlock textBlock)
                {
                    ApplyInlineText(textBlock.Inlines);
                }

                ApplyStaticText(element, TextBlock.TextProperty, GetText, SetText);
                ApplyStaticText(element, ToolTipService.ToolTipProperty, GetToolTip, SetToolTip);
                ApplyStaticText(element, AutomationProperties.NameProperty, GetAutomationName, SetAutomationName);
                ApplyStaticText(
                    element,
                    ContentControl.ContentStringFormatProperty,
                    GetContentStringFormat,
                    SetContentStringFormat);
                ApplyStaticText(
                    element,
                    ContentPresenter.ContentStringFormatProperty,
                    GetContentPresenterStringFormat,
                    SetContentPresenterStringFormat);
                ApplyHeader(element);
                ApplyContent(element);
                RefreshBoundText(element, TextBlock.TextProperty);
                RefreshBoundText(element, ToolTipService.ToolTipProperty);
                RefreshBoundText(element, AutomationProperties.NameProperty);
                RefreshBoundText(element, ContentControl.ContentProperty);
                RefreshBoundHeader(element);
            }
        }

        private static void ApplyDataGridColumnHeaders(DataGrid dataGrid)
        {
            foreach (DataGridColumn column in dataGrid.Columns)
            {
                if (!(column.Header is string header))
                {
                    continue;
                }

                OriginalColumnHeaderValue original = OriginalColumnHeaders.GetOrCreateValue(column);
                if (!original.HasValue)
                {
                    original.Value = header;
                    original.HasValue = true;
                }

                column.Header = Translate(original.Value);
            }
        }

        private static void ApplyInlineText(InlineCollection inlines)
        {
            foreach (Inline inline in inlines.Cast<Inline>().ToArray())
            {
                if (inline is Run run)
                {
                    ApplyStaticText(run, Run.TextProperty, GetRunText, SetRunText);
                    RefreshBoundText(run, Run.TextProperty);
                }
                else if (inline is Span span)
                {
                    ApplyInlineText(span.Inlines);
                }
            }
        }

        private static IEnumerable<DependencyObject> EnumerateVisualTree(DependencyObject root)
        {
            yield return root;
            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                foreach (DependencyObject child in EnumerateVisualTree(VisualTreeHelper.GetChild(root, i)))
                {
                    yield return child;
                }
            }
        }

        private static void ApplyWindowTitle(Window window)
        {
            if (BindingOperations.IsDataBound(window, Window.TitleProperty))
            {
                RefreshBoundText(window, Window.TitleProperty);
                return;
            }

            OriginalValueSet original = OriginalValues.GetOrCreateValue(window);
            if (!original.Values.ContainsKey(Window.TitleProperty))
            {
                original.Values[Window.TitleProperty] = window.Title;
            }

            window.SetCurrentValue(Window.TitleProperty, Translate(original.Values[Window.TitleProperty]));
        }

        private static void ApplyHeader(DependencyObject element)
        {
            if (!(element is HeaderedContentControl headered)
                || BindingOperations.IsDataBound(element, HeaderedContentControl.HeaderProperty)
                || !(headered.Header is string text))
            {
                return;
            }

            ApplyStaticValue(element, HeaderedContentControl.HeaderProperty, text, value => headered.SetCurrentValue(HeaderedContentControl.HeaderProperty, value));
        }

        private static void ApplyContent(DependencyObject element)
        {
            if (!(element is ButtonBase)
                && !(element is Label)
                && !(element is TabItem)
                && !(element is GroupBox)
                && !(element is Expander)
                && !(element is MenuItem))
            {
                return;
            }

            if (BindingOperations.IsDataBound(element, ContentControl.ContentProperty)
                || !(element.GetValue(ContentControl.ContentProperty) is string text))
            {
                return;
            }

            ApplyStaticValue(element, ContentControl.ContentProperty, text, value =>
                ((ContentControl)element).SetCurrentValue(ContentControl.ContentProperty, value));
        }

        private static void ApplyStaticText(
            DependencyObject element,
            DependencyProperty property,
            Func<DependencyObject, string> getter,
            Action<DependencyObject, string> setter)
        {
            if (property == TextBlock.TextProperty && !(element is TextBlock))
            {
                return;
            }

            if (BindingOperations.IsDataBound(element, property))
            {
                return;
            }

            string text = getter(element);
            if (text == null)
            {
                return;
            }

            ApplyStaticValue(element, property, text, value => setter(element, value));
        }

        private static void ApplyStaticValue(
            DependencyObject element,
            DependencyProperty property,
            string currentValue,
            Action<string> setter)
        {
            OriginalValueSet original = OriginalValues.GetOrCreateValue(element);
            if (!original.Values.ContainsKey(property))
            {
                original.Values[property] = currentValue;
            }

            setter(Translate(original.Values[property]));
        }

        private static void RefreshBoundText(DependencyObject element, DependencyProperty property)
        {
            BindingExpression expression = BindingOperations.GetBindingExpression(element, property);
            if (expression == null || !ShouldLocalizeBinding(element, expression.ParentBinding))
            {
                return;
            }

            BindingRecordSet records = BindingRecords.GetOrCreateValue(element);
            Binding binding = records.Bindings.TryGetValue(property, out Binding originalBinding)
                ? originalBinding
                : expression.ParentBinding;
            records.Bindings[property] = binding;
            Binding localizedBinding = CloneBinding(binding);
            localizedBinding.Converter = binding.Converter is LocalizedValueConverter localizedConverter
                ? localizedConverter
                : new LocalizedValueConverter(binding.Converter);
            localizedBinding.StringFormat = Translate(binding.StringFormat);

            string contentStringFormat = null;
            if (element is ContentControl contentControl
                && property == ContentControl.ContentProperty)
            {
                contentStringFormat = contentControl.ContentStringFormat;
                if (contentStringFormat != null)
                {
                    contentControl.SetCurrentValue(ContentControl.ContentStringFormatProperty, null);
                }
            }

            BindingOperations.SetBinding(element, property, localizedBinding);
            if (contentStringFormat != null
                && element is ContentControl formattedContentControl)
            {
                formattedContentControl.SetCurrentValue(
                    ContentControl.ContentStringFormatProperty,
                    contentStringFormat);
            }

            expression = BindingOperations.GetBindingExpression(element, property);

            expression.UpdateTarget();
        }

        private static Binding CloneBinding(Binding source)
        {
            Binding clone;
            if (source.RelativeSource != null)
            {
                clone = new Binding { RelativeSource = source.RelativeSource };
            }
            else if (!string.IsNullOrWhiteSpace(source.ElementName))
            {
                clone = new Binding { ElementName = source.ElementName };
            }
            else if (source.Source != null)
            {
                clone = new Binding { Source = source.Source };
            }
            else
            {
                clone = new Binding();
            }

            clone.Path = source.Path;
            clone.XPath = source.XPath;
            clone.Mode = source.Mode;
            clone.UpdateSourceTrigger = source.UpdateSourceTrigger;
            clone.BindsDirectlyToSource = source.BindsDirectlyToSource;
            clone.NotifyOnSourceUpdated = source.NotifyOnSourceUpdated;
            clone.NotifyOnTargetUpdated = source.NotifyOnTargetUpdated;
            clone.NotifyOnValidationError = source.NotifyOnValidationError;
            clone.ValidatesOnDataErrors = source.ValidatesOnDataErrors;
            clone.ValidatesOnExceptions = source.ValidatesOnExceptions;
            clone.ValidatesOnNotifyDataErrors = source.ValidatesOnNotifyDataErrors;
            clone.IsAsync = source.IsAsync;
            clone.AsyncState = source.AsyncState;
            clone.Delay = source.Delay;
            clone.ConverterCulture = source.ConverterCulture;
            clone.ConverterParameter = source.ConverterParameter;
            clone.StringFormat = source.StringFormat;
            clone.TargetNullValue = source.TargetNullValue;
            clone.FallbackValue = source.FallbackValue;
            clone.UpdateSourceExceptionFilter = source.UpdateSourceExceptionFilter;
            foreach (ValidationRule rule in source.ValidationRules)
            {
                clone.ValidationRules.Add(rule);
            }

            return clone;
        }

        private static bool ShouldLocalizeBinding(DependencyObject element, Binding binding)
        {
            string path = binding?.Path?.Path ?? string.Empty;
            if (path.IndexOf("FileName", StringComparison.OrdinalIgnoreCase) >= 0
                && path.IndexOf("ImageName", StringComparison.OrdinalIgnoreCase) < 0
                && path.IndexOf("RecipeName", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            if (path.IndexOf("ImageName", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("RecipeName", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("ClassName", StringComparison.OrdinalIgnoreCase) >= 0
                || (element is FrameworkElement frameworkElement
                    && frameworkElement.DataContext is WpfCanvasLabelClassItem))
            {
                return false;
            }

            return true;
        }

        private static void RefreshBoundHeader(DependencyObject element)
        {
            BindingExpression expression = BindingOperations.GetBindingExpression(
                element,
                HeaderedContentControl.HeaderProperty);
            if (expression == null || !ShouldLocalizeBinding(element, expression.ParentBinding))
            {
                return;
            }

            expression.UpdateTarget();
        }

        private static string GetText(DependencyObject element) => element.GetValue(TextBlock.TextProperty) as string;
        private static void SetText(DependencyObject element, string value) => ((TextBlock)element).SetCurrentValue(TextBlock.TextProperty, value);
        private static string GetRunText(DependencyObject element) => element.GetValue(Run.TextProperty) as string;
        private static void SetRunText(DependencyObject element, string value) => ((Run)element).SetCurrentValue(Run.TextProperty, value);
        private static string GetToolTip(DependencyObject element) => element.GetValue(ToolTipService.ToolTipProperty) as string;
        private static void SetToolTip(DependencyObject element, string value) => element.SetCurrentValue(ToolTipService.ToolTipProperty, value);
        private static string GetAutomationName(DependencyObject element) => element.GetValue(AutomationProperties.NameProperty) as string;
        private static void SetAutomationName(DependencyObject element, string value) => element.SetCurrentValue(AutomationProperties.NameProperty, value);
        private static string GetContentStringFormat(DependencyObject element)
            => element.GetValue(ContentControl.ContentStringFormatProperty) as string;
        private static void SetContentStringFormat(DependencyObject element, string value)
            => element.SetCurrentValue(ContentControl.ContentStringFormatProperty, value);
        private static string GetContentPresenterStringFormat(DependencyObject element)
            => element.GetValue(ContentPresenter.ContentStringFormatProperty) as string;
        private static void SetContentPresenterStringFormat(DependencyObject element, string value)
            => element.SetCurrentValue(ContentPresenter.ContentStringFormatProperty, value);

        private sealed class OriginalValueSet
        {
            public Dictionary<DependencyProperty, string> Values { get; } = new Dictionary<DependencyProperty, string>();
        }

        private sealed class OriginalColumnHeaderValue
        {
            public bool HasValue { get; set; }
            public string Value { get; set; }
        }

        private sealed class BindingRecordSet
        {
            public Dictionary<DependencyProperty, Binding> Bindings { get; } = new Dictionary<DependencyProperty, Binding>();
        }

        private sealed class RootRefreshState
        {
            public bool Pending { get; set; }
            public bool Queued { get; set; }
            public DispatcherTimer DelayedRefreshTimer { get; set; }
            public EventHandler DelayedRefreshHandler { get; set; }
        }

        private sealed class LocalizedValueConverter : IValueConverter
        {
            private readonly IValueConverter innerConverter;

            public LocalizedValueConverter(IValueConverter innerConverter)
            {
                this.innerConverter = innerConverter;
            }

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                object converted = innerConverter?.Convert(value, targetType, parameter, culture) ?? value;
                if (!(converted is string text)
                    || string.Equals(text, string.Empty, StringComparison.Ordinal))
                {
                    return converted;
                }

                return Translate(text);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => innerConverter?.ConvertBack(value, targetType, parameter, culture) ?? value;
        }
    }
}
