using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace CapMonitor
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
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
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            this.Topmost = true;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            this.Topmost = true;
            this.Activate();

            FLASHWINFO fInfo = new FLASHWINFO();
            fInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(fInfo));
            fInfo.hwnd = new WindowInteropHelper(this).Handle;
            fInfo.dwFlags = 0;
            fInfo.uCount = UInt32.MaxValue;
            fInfo.dwTimeout = 0;
            FlashWindowEx(ref fInfo);
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
                foreach (var recentTran in RecentTransactions["ReturnData"])
                {
                    if (mLastTransactionID == recentTran["TransactionID"].ToString())
                        break;

                    string log = "";
                    log += (recentTran["ResentType"].ToString() == "Bid") ? "매수" : "매도";
                    log += " ";
                    log += recentTran["TransactionDate"];
                    log += "    ";
                    log += recentTran["Price"].ToString().Substring(0, 4);
                    log += "    ";
                    log += InsertComma(recentTran["UnitTraded"].ToString().Substring(0,
                        recentTran["UnitTraded"].ToString().IndexOf(".")));
                    li.Add(log);
                }

                Dispatcher.Invoke((Action)delegate
                {
                    li.Reverse();
                    foreach (var line in li)
                        lb_recentPayment.Items.Add(line);
                });

                mLastTransactionID = lastTransactionID;

                Dispatcher.Invoke((Action)delegate
                {
                    if (VisualTreeHelper.GetChildrenCount(lb_recentPayment) > 0)
                    {
                        Border border = (Border)VisualTreeHelper.GetChild(lb_recentPayment, 0);
                        ScrollViewer scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
                        scrollViewer.ScrollToBottom();
                    }
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