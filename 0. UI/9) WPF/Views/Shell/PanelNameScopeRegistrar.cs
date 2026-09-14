using System;
using System.Reflection;
using System.Windows;

namespace MvcVisionSystem
{
    /// <summary>
    /// Registers nested panel accessor properties in the shell namescope.
    /// This is a WPF compatibility adapter; panel workflow policy remains in
    /// the owning ViewModels and services.
    /// </summary>
    internal static class PanelNameScopeRegistrar
    {
        private const BindingFlags AccessorFlags = BindingFlags.Instance
            | BindingFlags.NonPublic
            | BindingFlags.Public
            | BindingFlags.DeclaredOnly;

        internal static void Register(FrameworkElement namescopeOwner)
        {
            ArgumentNullException.ThrowIfNull(namescopeOwner);

            foreach (PropertyInfo property in namescopeOwner.GetType().GetProperties(AccessorFlags))
            {
                if (!typeof(FrameworkElement).IsAssignableFrom(property.PropertyType))
                {
                    continue;
                }

                FrameworkElement element = property.GetValue(namescopeOwner) as FrameworkElement;
                if (element == null || namescopeOwner.FindName(property.Name) != null)
                {
                    continue;
                }

                namescopeOwner.RegisterName(property.Name, element);
            }
        }
    }
}
