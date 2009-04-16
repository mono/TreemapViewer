using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;
using Moonlight;
using System.IO;

namespace SilverlightViewer
{
    public partial class App : Application
    {

        public App()
        {
            this.Startup += this.Application_Startup;
            this.Exit += this.Application_Exit;
            this.UnhandledException += this.Application_UnhandledException;

            InitializeComponent();
        }


        public static Node LoadNodes(String s, string dim1, string dim2)
        {
            StringReader sr = new StringReader(s);

            XDocument d = XDocument.Load(sr);
            XElement root = ((XElement)d.FirstNode);
            XName d1 = XName.Get(dim1, "");
            XName d2 = XName.Get(dim2, "");

            return LoadNodes(root, d1, d2);
        }

        public static void Update(XElement xe, ref int v, XName k)
        {
            var attr = xe.Attribute(k);
            if (attr != null) {
                int r;

                if (int.TryParse(attr.Value, out r))
                    v += r;
            }
        }

        static XName xn_name = XName.Get("Name", "");
        
        public static Node LoadNodes(XElement xe, XName k1, XName k2)
        {
            XAttribute xa = xe.Attribute(xn_name);

            Node n = new Node(xe.Nodes().Count());
            if (xa != null)
                n.Name = xa.Value;

            Update(xe, ref n.Size, k1);
            Update(xe, ref n.Value, k2);

            foreach (XNode e in xe.Nodes()) {
                if (e is XElement) {
                    Node child = LoadNodes((XElement)e, k1, k2);
                    n.Size += child.Size;
                    n.Value += child.Value;

                    n.Children.Add(child);
                }
            }
            return n;
        }

        TreemapRenderer treemap;
        MainPage main;

        void LoadNodesFromString(String s)
        {
            Node n = LoadNodes(s, "Size", "Extra");

            Canvas c = main.CanvasHost;
            treemap = new TreemapRenderer(n, new Rect(0, 0, c.Width, c.Height), "Dingus");

            main.CanvasHost.Children.Add(treemap);
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            main = new MainPage();
            main.BackButton.Click += delegate {
                if (treemap != null)
                    treemap.Back();
            };

            this.RootVisual = main;
            
            var webclient = new WebClient();
            webclient.DownloadStringCompleted += delegate (object sender2, DownloadStringCompletedEventArgs ee){
                RootVisual.Dispatcher.BeginInvoke (() => LoadNodesFromString (ee.Result));
            };
            string s = App.Current.Host.Source.AbsolutePath;
            int p = s.LastIndexOf('/');
            s = s.Substring(0, p);
            
            webclient.DownloadStringAsync(new Uri("mscorlib.xml", UriKind.Relative));
        }

        void webclient_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void Application_Exit(object sender, EventArgs e)
        {

        }
        private void Application_UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            // If the app is running outside of the debugger then report the exception using
            // the browser's exception mechanism. On IE this will display it a yellow alert 
            // icon in the status bar and Firefox will display a script error.
            if (!System.Diagnostics.Debugger.IsAttached) {

                // NOTE: This will allow the application to continue running after an exception has been thrown
                // but not handled. 
                // For production applications this error handling should be replaced with something that will 
                // report the error to the website and stop the application.
                e.Handled = true;
                Deployment.Current.Dispatcher.BeginInvoke(delegate { ReportErrorToDOM(e); });
            }
        }
        private void ReportErrorToDOM(ApplicationUnhandledExceptionEventArgs e)
        {
            try {
                string errorMsg = e.ExceptionObject.Message + e.ExceptionObject.StackTrace;
                errorMsg = errorMsg.Replace('"', '\'').Replace("\r\n", @"\n");

                System.Windows.Browser.HtmlPage.Window.Eval("throw new Error(\"Unhandled Error in Silverlight Application " + errorMsg + "\");");
            }
            catch (Exception) {
            }
        }
    }
}
