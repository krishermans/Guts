﻿using System.Windows.Controls;
using System.Windows.Input;

namespace Guts.Client.TestTools.WPF
{
    public static class ControlExtensions
    {
        public static void FireDoubleClickEvent(this Control control)
        {
            var doubleClickEventArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = Control.MouseDoubleClickEvent
            };
            control.RaiseEvent(doubleClickEventArgs);
        }
    }
}