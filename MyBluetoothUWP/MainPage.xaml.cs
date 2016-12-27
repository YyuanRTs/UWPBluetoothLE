using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace MyBluetoothUWP
{
    public class VisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool && ((bool)value))
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class VisibilityRConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool && ((bool)value))
                return Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if ((bool)value)
            {
                return "Paired";
            }
            else
            {
                return "NeedToPair";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolReverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return !((bool)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }


    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            BaseGrid.Visibility = Visibility.Collapsed;
            AddDeviceGrid.Visibility = Visibility.Visible;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            BaseGrid.Visibility = Visibility.Visible;
            AddDeviceGrid.Visibility = Visibility.Collapsed;
        }

        //private void SearchingList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    BaseGrid.Visibility = Visibility.Visible;
        //    AddDeviceGrid.Visibility = Visibility.Collapsed;
        //}

        private void SearchingList_ItemClick(object sender, ItemClickEventArgs e)
        {
            BaseGrid.Visibility = Visibility.Visible;
            AddDeviceGrid.Visibility = Visibility.Collapsed;
        }
    }
}
