using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace CapMonitor
{
    public class StringToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value != null && value.ToString().StartsWith("Buy"))
                return "#ffa07a";
            else
                return "#87cefa";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        public struct FLASHWINFO
        {
            public UInt32 cbSize;
            public IntPtr hwnd;
            public UInt32 dwFlags;
            public UInt32 uCount;
            public UInt32 dwTimeout;
        }

        private const UInt32 FLASHW_STOP = 0; //Stop flashing. The system restores the window to its original state.
        private const UInt32 FLASHW_CAPTION = 1; //Flash the window caption.        
        private const UInt32 FLASHW_TRAY = 2; //Flash the taskbar button.        
        private const UInt32 FLASHW_ALL = 3; //Flash both the window caption and taskbar button.        
        private const UInt32 FLASHW_TIMER = 4; //Flash continuously, until the FLASHW_STOP flag is set.        
        private const UInt32 FLASHW_TIMERNOFG = 12; //Flash continuously until the window comes to the foreground.  

        public static bool FlashWindowEx(IntPtr hWnd)
        {
            FLASHWINFO fInfo = new FLASHWINFO();

            fInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(fInfo));
            fInfo.hwnd = hWnd;
            fInfo.dwFlags = 0;
            fInfo.uCount = 0;
            fInfo.dwTimeout = 0;

            return FlashWindowEx(ref fInfo);
        }

        public static void FlashWindow(Window win, UInt32 count = UInt32.MaxValue)
        {
            //Don't flash if the window is active            
            if (win.IsActive) return;
            WindowInteropHelper h = new WindowInteropHelper(win);
            FLASHWINFO info = new FLASHWINFO
            {
                hwnd = h.Handle,
                dwFlags = FLASHW_ALL | FLASHW_TIMER,
                uCount = count,
                dwTimeout = 0
            };

            info.cbSize = Convert.ToUInt32(Marshal.SizeOf(info));
            FlashWindowEx(ref info);
        }

        public static void StopFlashingWindow(Window win)
        {
            WindowInteropHelper h = new WindowInteropHelper(win);
            FLASHWINFO info = new FLASHWINFO();
            info.hwnd = h.Handle;
            info.cbSize = Convert.ToUInt32(Marshal.SizeOf(info));
            info.dwFlags = FLASHW_STOP;
            info.uCount = UInt32.MaxValue;
            info.dwTimeout = 0;
            FlashWindowEx(ref info);
        }

        private static System.Timers.Timer mUpdateRecentTransactionTimer;
        private static System.Timers.Timer mUpdateOrderBookTimer;
        private string mLastTransactionID = "";

        public MainWindow()
        {
            InitializeComponent();

            mUpdateRecentTransactionTimer = new System.Timers.Timer(2500);
            mUpdateRecentTransactionTimer.Elapsed += _UpdateRecentTransaction;
            mUpdateRecentTransactionTimer.AutoReset = true;
            mUpdateRecentTransactionTimer.Enabled = true;

            mUpdateOrderBookTimer = new System.Timers.Timer(2500);
            mUpdateOrderBookTimer.Elapsed += _UpdateOrderBook;
            mUpdateOrderBookTimer.AutoReset = true;
            mUpdateOrderBookTimer.Enabled = true;

            this.Topmost = true;
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            StopFlashingWindow(this);
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
        }

        public string InsertComma(string number)
        {
            int commaCnt = number.Length / 3;
            for (int i = 0; i < commaCnt; ++i)
            {
                int commaPos = number.Length - (3 + (3 * i)) - i;
                if (commaPos <= 0)
                    break;
                number = number.Insert(commaPos, ",");
            }
            return number;
        }

        private void _UpdateRecentTransaction(Object source, ElapsedEventArgs e)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://rest.cashierest.com/Public/RecentTransactions?Coin=CAP");
            request.Method = "GET";

            try
            {
                WebResponse webResponse = request.GetResponse();
                Stream webStream = webResponse.GetResponseStream();
                StreamReader responseReader = new StreamReader(webStream);
                string response = responseReader.ReadToEnd();
                var RecentTransactions = JObject.Parse(response);
                //Console.Out.WriteLine(response);
                responseReader.Close();

                var recentTransactionData = RecentTransactions["ReturnData"][0];
                if (mLastTransactionID == recentTransactionData["TransactionID"].ToString())
                    return;

                Dispatcher.Invoke((Action)delegate
                {
                    tb_recentPrice.Text = recentTransactionData["Price"].ToString().Substring(0, 4);
                });

                List<string> li = new List<string>();
                string lastTransactionID = recentTransactionData["TransactionID"].ToString();
                float prevPrice = -1, currPrice = -1;
                var returnData = RecentTransactions["ReturnData"];
                foreach (var recentTran in RecentTransactions["ReturnData"])
                {
                    string priceStr = recentTran["Price"].ToString();
                    priceStr = priceStr.Substring(0, priceStr.IndexOf(".") + 3);
                    currPrice = float.Parse(priceStr);

                    if (recentTran.Next != null)
                    {
                        prevPrice = float.Parse(recentTran.Next["Price"].ToString().Substring(0,
                            recentTran.Next["Price"].ToString().IndexOf(".") + 3));
                    }
                    else
                        prevPrice = -1;

                    if (mLastTransactionID == recentTran["TransactionID"].ToString())
                        break;

                    string log = "";
                    log += (recentTran["ResentType"].ToString() == "Bid") ? "Buy" : "Sell";
                    log += "    ";
                    log += recentTran["TransactionDate"].ToString().Substring(5);
                    log += "    ";
                    if (prevPrice == -1 || prevPrice == currPrice) { log += "〓"; }
                    else if (prevPrice < currPrice) { log += "△"; }
                    else { log += "▽"; }
                    log += " ";
                    log += priceStr.Substring(0, priceStr.IndexOf(".") + 3);
                    log += "    ";
                    log += InsertComma(recentTran["UnitTraded"].ToString().Substring(0,
                        recentTran["UnitTraded"].ToString().IndexOf(".")));
                    li.Add(log);
                    prevPrice = currPrice;
                }

                mLastTransactionID = lastTransactionID;

                Dispatcher.Invoke((Action)delegate
                {
                    li.Reverse();
                    foreach (var line in li)
                        lb_recentPayment.Items.Add(line);

                    if (VisualTreeHelper.GetChildrenCount(lb_recentPayment) > 0)
                    {
                        Border border = (Border)VisualTreeHelper.GetChild(lb_recentPayment, 0);
                        ScrollViewer scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
                        scrollViewer.ScrollToBottom();
                    }

                    if (li.Count > 0)
                        FlashWindow(this);
                });

            }
            catch (Exception exception)
            {
                Dispatcher.Invoke((Action)delegate
                {
                    lb_recentPayment.Items.Add(exception.Message);
                });
            }
        }

        private void _UpdateOrderBook(Object source, ElapsedEventArgs e)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://rest.cashierest.com/Public/OrderBook?Coin=CAP");
            request.Method = "GET";

            try
            {
                WebResponse webResponse = request.GetResponse();
                Stream webStream = webResponse.GetResponseStream();
                StreamReader responseReader = new StreamReader(webStream);
                string response = responseReader.ReadToEnd();
                var orderBook = JObject.Parse(response)["ReturnData"];
                //Console.Out.WriteLine(response);
                responseReader.Close();

                Dispatcher.Invoke((Action)delegate
                {
                    lb_bid1.Content = orderBook["Bids"][0]["Price"].ToString().Substring(0, 4);
                    lb_bid2.Content = orderBook["Bids"][1]["Price"].ToString().Substring(0, 4);
                    lb_bid3.Content = orderBook["Bids"][2]["Price"].ToString().Substring(0, 4);
                    lb_bid4.Content = orderBook["Bids"][3]["Price"].ToString().Substring(0, 4);
                    lb_bid5.Content = orderBook["Bids"][4]["Price"].ToString().Substring(0, 4);

                    string num = orderBook["Bids"][0]["Quantity"].ToString();
                    tb_bid1.Text = InsertComma(num.Substring(0, num.IndexOf(".")));
                    num = orderBook["Bids"][1]["Quantity"].ToString();
                    tb_bid2.Text = InsertComma(num.Substring(0, num.IndexOf(".")));
                    num = orderBook["Bids"][2]["Quantity"].ToString();
                    tb_bid3.Text = InsertComma(num.Substring(0, num.IndexOf(".")));
                    num = orderBook["Bids"][3]["Quantity"].ToString();
                    tb_bid4.Text = InsertComma(num.Substring(0, num.IndexOf(".")));
                    num = orderBook["Bids"][4]["Quantity"].ToString();
                    tb_bid5.Text = InsertComma(num.Substring(0, num.IndexOf(".")));

                    lb_ask1.Content = orderBook["Asks"][0]["Price"].ToString().Substring(0, 4);
                    lb_ask2.Content = orderBook["Asks"][1]["Price"].ToString().Substring(0, 4);
                    lb_ask3.Content = orderBook["Asks"][2]["Price"].ToString().Substring(0, 4);
                    lb_ask4.Content = orderBook["Asks"][3]["Price"].ToString().Substring(0, 4);
                    lb_ask5.Content = orderBook["Asks"][4]["Price"].ToString().Substring(0, 4);

                    num = orderBook["Asks"][0]["Quantity"].ToString();
                    tb_ask1.Text = InsertComma(num.Substring(0, num.IndexOf(".")));
                    num = orderBook["Asks"][1]["Quantity"].ToString();
                    tb_ask2.Text = InsertComma(num.Substring(0, num.IndexOf(".")));
                    num = orderBook["Asks"][2]["Quantity"].ToString();
                    tb_ask3.Text = InsertComma(num.Substring(0, num.IndexOf(".")));
                    num = orderBook["Asks"][3]["Quantity"].ToString();
                    tb_ask4.Text = InsertComma(num.Substring(0, num.IndexOf(".")));
                    num = orderBook["Asks"][4]["Quantity"].ToString();
                    tb_ask5.Text = InsertComma(num.Substring(0, num.IndexOf(".")));
                });
            }
            catch (Exception exception)
            {
                Dispatcher.Invoke((Action)delegate
                {
                    lb_recentPayment.Items.Add(exception.Message);
                });
            }
        }
    }
}