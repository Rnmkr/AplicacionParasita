using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Timers;
using System.Windows;
using VisorDataLibrary;

namespace AplicacionParasita
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Timer _timer;
        private string _TEXTFILEPATH;
        private string _RATEFILEPATH;
        private string _EXEFILEPATH;
        private string _LOGFILEPATH;
        private int _SEGUNDOSREFRESCO = 20;
        private int counter;
        private string _promedio = "0";
        private int _counterPromedio = 3;

        public MainWindow()
        {
            InitializeComponent();

            this.Left = SystemParameters.PrimaryScreenWidth - this.Width;

            try
            {

                _timer = new Timer(1000) { AutoReset = true }; //espera 20 segundos (+10 de espera refrescando) para enviar datos a la pantalla.
                _timer.Elapsed += (sender, eventArgs) => EnviarDatos();
                _timer.Start();
                string localdir = Directory.GetParent(Assembly.GetExecutingAssembly().Location).ToString();
                _TEXTFILEPATH = Path.Combine(Directory.GetParent(localdir).ToString(), "HIDE", "FORMATO.TXT");
                _RATEFILEPATH = Path.Combine(Directory.GetParent(localdir).ToString(), "HIDE", DateTime.Today.ToString("yyyy-MM-dd") + ".PRM");
                _EXEFILEPATH = Path.Combine(localdir, "Comunicador.exe");
                _LOGFILEPATH = Path.Combine(localdir, "AplicacionParasita.apl");

                labelVER.Content = "v." + Assembly.GetExecutingAssembly().GetName().Version.ToString();
                counter = _SEGUNDOSREFRESCO;
                labelIndicador.Content = counter;

            }
            catch (Exception e)
            {
                EscribirLog("LD: " + e.Message);
                labelIndicador.Background = System.Windows.Media.Brushes.Red;
                labelTexto.Content = "NO SE ENVIAN DATOS AL DISPLAY";
            }
        }

        private void EnviarDatos()
        {
            counter--;

            if (counter == 0)
            {
                counter = _SEGUNDOSREFRESCO;
                try
                {
                    RefrescarDatos();
                    var lineas = ReadLines(_TEXTFILEPATH);
                    var datosVisor = ObtenerDatosVisor(lineas);
                    SendMulticast(datosVisor);
                    labelIndicador.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                       new Action(() => { labelIndicador.Background = System.Windows.Media.Brushes.LawnGreen; }));
                    labelIndicador.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                       new Action(() => { labelTexto.Content = "ENVIANDO DATOS AL DISPLAY"; }));
                }
                catch (Exception e)
                {
                    EscribirLog("SD: " + e.Message);
                    labelIndicador.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                                       new Action(() => { labelIndicador.Background = System.Windows.Media.Brushes.Red; }));
                    labelIndicador.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                       new Action(() => { labelTexto.Content = "NO SE ENVIARON DATOS AL DISPLAY"; }));
                }
            }
            labelIndicador.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
               new Action(() => { labelIndicador.Content = counter; }));
        }

        private void RefrescarDatos()
        {
            try
            {
                _counterPromedio--;

                if (_counterPromedio == 0)
                {
                    DateTime creation = File.GetCreationTime(_RATEFILEPATH);
                    float elapsed = DateTime.Now.Subtract(creation).Minutes;
                    Nullable<float> linesNullable = File.ReadAllLines(_RATEFILEPATH).Length;
                    float lines = linesNullable.HasValue ? linesNullable.Value : 0;
                    float rate = (float)Math.Round((lines / elapsed), 2);
                    _promedio = rate.ToString();
                    _counterPromedio = 3;
                }

                Process process = new Process();
                process.StartInfo.FileName = _EXEFILEPATH;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.Start();
                process.WaitForExit(10 * 1000);    // Esperar 10 segundos
            }
            catch (Exception e)
            {
                EscribirLog("RD: " + e.Message);
                labelIndicador.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                   new Action(() => { labelIndicador.Background = System.Windows.Media.Brushes.Red; }));
                labelIndicador.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                   new Action(() => { labelTexto.Content = "NO SE ENVIAN DATOS AL DISPLAY"; }));
            }

        }

        private void EscribirLog(string err)
        {
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(_LOGFILEPATH, true))
            {
                file.WriteLine("=========================" + DateTime.Now + "=========================");
                file.WriteLine(err);
                file.WriteLine("");
            }
        }

        public static string[] ReadLines(string path)
        {
            string[] lineArray = new string[16];

            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 0x1000, FileOptions.SequentialScan))
            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
            {
                string line;
                int index = 0;

                while ((line = streamReader.ReadLine()) != null)
                {
                    lineArray[index] = line.Substring(line.LastIndexOf("=") + 1);
                    index++;
                }
            }
            return lineArray;
        }

        private VisorData ObtenerDatosVisor(string[] lineas)
        {

            var datosVisor = new VisorData();

            datosVisor.Linea = "--";
            datosVisor.Supervisor = "----------";
            datosVisor.PedidoEnProduccion = "-----------";
            datosVisor.PedidoAnterior = "-----------";
            datosVisor.PedidoSiguiente = "-----------";
            datosVisor.TotalUnidadesPedido = "--";
            datosVisor.Producto = "----------";
            datosVisor.Modelo = "----------";
            datosVisor.UnidadesTesteadas = "--";
            datosVisor.TesteadoresDesignados = "";
            datosVisor.UltimasTesteadas = "";
            datosVisor.UnidadesEmbaladas = "--";
            datosVisor.EmbaladoresDesignados = "";
            datosVisor.UltimasEmbaladas = "";
            datosVisor.ParcialTest = "--";
            datosVisor.ParcialEmbalado = "--";
            datosVisor.EstadoPedido = "----------";
            datosVisor.Promedio = "----";

            datosVisor.Linea = lineas[0];
            datosVisor.Supervisor = lineas[1];
            datosVisor.PedidoEnProduccion = lineas[2];
            datosVisor.PedidoAnterior = lineas[3];
            //datosVisor.PedidoSiguiente = lineas[4]; //reemplazado temporalmente por "Promedio"
            datosVisor.TotalUnidadesPedido = lineas[5];

            string pro = lineas[6];
            pro = pro.Replace("_", " ").ToUpper().TrimEnd();
            datosVisor.Producto = pro;

            string mod = lineas[7];
            mod = mod.Replace("_", " ").ToUpper().TrimEnd();
            datosVisor.Modelo = mod;

            datosVisor.UnidadesTesteadas = lineas[8];

            datosVisor.TesteadoresDesignados = FormatearNombres(lineas[9]);

            datosVisor.UltimasTesteadas = lineas[10];
            datosVisor.UnidadesEmbaladas = lineas[11];

            datosVisor.EmbaladoresDesignados = FormatearNombres(lineas[12]);


            datosVisor.UltimasEmbaladas = lineas[13];
            datosVisor.ParcialTest = lineas[5]; //por ahora es siempre igual al total del pedido ingresado
            datosVisor.ParcialEmbalado = lineas[5]; //por ahora es siempre igual al total del pedido ingresado
            datosVisor.EstadoPedido = lineas[14].ToUpper().TrimEnd();

            datosVisor.Promedio = _promedio;

            return datosVisor;
        }

        private string FormatearNombres(string s)
        {
            string[] strArray = s.ToUpper().Split('·');

            string names = "·";

            foreach (string item in strArray)
            {
                string[] subArray = item.Trim().Split(' ');

                if (subArray.Length < 2) continue;

                for (int i = 0; i < 2; i++)
                {
                    names += " " + subArray[i];
                }

                names += " ·";
            }

            return names;
        }

        private void SendMulticast(VisorData datos)
        {
            //enviar por udp
            UdpClient udpclient = new UdpClient();
            IPAddress multicastaddress = IPAddress.Parse("239.239.239.239");
            udpclient.JoinMulticastGroup(multicastaddress);
            IPEndPoint remoteep = new IPEndPoint(multicastaddress, 7001);
            byte[] buffer = null;
            Encoding enc = Encoding.Unicode;
            buffer = ObjectToByteArray(datos);
            udpclient.Send(buffer, buffer.Length, remoteep);
        }

        public static byte[] ObjectToByteArray(VisorData dataToSend)
        {
            BinaryFormatter bf = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                bf.Serialize(ms, dataToSend);
                return ms.ToArray();
            }
        }
    }
}
