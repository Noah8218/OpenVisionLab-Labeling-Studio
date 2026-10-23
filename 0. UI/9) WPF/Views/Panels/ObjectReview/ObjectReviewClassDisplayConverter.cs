using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    public sealed class ObjectReviewClassDisplayConverter : DependencyObject, IMultiValueConverter
    {
        public static readonly DependencyProperty ClassesProperty = DependencyProperty.Register(nameof(Classes), typeof(IEnumerable<WpfCanvasLabelClassItem>), typeof(ObjectReviewClassDisplayConverter), new PropertyMetadata(null, OnClassesChanged));
        private static readonly DependencyPropertyKey RevisionPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Revision), typeof(int), typeof(ObjectReviewClassDisplayConverter), new PropertyMetadata(0));
        public static readonly DependencyProperty RevisionProperty = RevisionPropertyKey.DependencyProperty;

        public IEnumerable<WpfCanvasLabelClassItem> Classes
        {
            get => (IEnumerable<WpfCanvasLabelClassItem>)GetValue(ClassesProperty);
            set => SetValue(ClassesProperty, value);
        }

        public int Revision => (int)GetValue(RevisionProperty);

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string name = values.Length > 0 ? values[0] as string ?? string.Empty : string.Empty;
            var classes = values.Length > 1 ? values[1] as IEnumerable<WpfCanvasLabelClassItem> : null;
            WpfCanvasLabelClassItem item = classes?.FirstOrDefault(value => string.Equals(value.Text, ClassCatalogService.NormalizeClassName(name), StringComparison.OrdinalIgnoreCase));
            return item == null ? name : $"{item.CanonicalIndex + 1} {name}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();

        private static void OnClassesChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
        {
            var converter = (ObjectReviewClassDisplayConverter)owner;
            if (args.OldValue is INotifyCollectionChanged previous) CollectionChangedEventManager.RemoveHandler(previous, converter.OnCollectionChanged);
            if (args.NewValue is INotifyCollectionChanged current) CollectionChangedEventManager.AddHandler(current, converter.OnCollectionChanged);
            converter.AdvanceRevision();
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args) => AdvanceRevision();

        private void AdvanceRevision() => SetValue(RevisionPropertyKey, Revision + 1);
    }
}
