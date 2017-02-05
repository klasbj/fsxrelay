using System;
using System.Globalization;
using System.Threading;
using BeatlesBlog.SimConnect;

namespace fsx_relay_console
{
     // Structs for SimConnect
    [DataStruct()]
    public struct AIData
    {
        [DataItem("Attitude Indicator Pitch Degrees", "degrees")]
        public float pitch;
            
        [DataItem("Attitude Indicator Bank Degrees", "degrees")]
        public float bank;

        [DataItem("Attitude Bars Position", "percent")]
        public float bars_position;

        [DataItem("Attitude Cage", "bool")]
        public bool caged;

        public string ToJson()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");
            return String.Format("{{\"pitch\":{0:G10},\"bank\":{1:G10},\"bars_position\":{2:G10},\"caged\":{3} }}",
                this.pitch, this.bank, this.bars_position, this.caged ? "true" : "false");
        }
    }

    [DataStruct()]
    public struct AirspeedData
    {
        [DataItem("Airspeed Indicated", "knots")]
        public float kias;

        [DataItem("Airspeed True", "knots")]
        public float ktas;

        [DataItem("Airspeed True Calibrate", "degrees")]
        public float ktas_calibrate;

        [DataItem("Airspeed Barber Pole", "knots")]
        public float k_barber_pole;

        [DataItem("Barber Pole Mach", "mach")]
        public float m_barber_pole;

        [DataItem("Mach Max Operate", "mach")]
        public float mmo;

        [DataItem("Airspeed Mach", "mach")]
        public float mach;

        [DataItem("Design Speed Vs0", "knots")]
        public float vs0;

        [DataItem("Design Speed Vs1", "knots")]
        public float vs1;

        [DataItem("Design Speed Vc", "knots")]
        public float vc;
    }

    [DataStruct()]
    public struct AltitudeData
    {
        [DataItem("Indicated Altitude", "feet")]
        public float altitude;

        [DataItem("Kohlsman Setting Mb", "millibars")]
        public float altimiter_setting;

        [DataItem("Vertical Speed", "feet/minute")]
        public float vertical_speed;
    }

    [DataStruct()]
    public struct InstrumentData
    {
        [DataItem()]
        public AIData instrumentdata;

        [DataItem()]
        public AirspeedData airspeed;

        [DataItem()]
        public AltitudeData altitude;
    }

    public delegate void InstrumentDataHandler(ISimClient client, InstrumentData data);
    public delegate void DisconnectHandler();
    public interface ISimClient
    {
        event InstrumentDataHandler InstrumentDataReceived;
        event DisconnectHandler Disconnected;

        void Open(string name);
        void Open(string name, string hostName, int port, bool localIfAble = true);
        void Close();
    }

    public class DummySimClient : ISimClient
    {
        public event InstrumentDataHandler InstrumentDataReceived = delegate {};
        public event DisconnectHandler Disconnected;

        private InstrumentData instrumentData = new InstrumentData();
        private Timer timer = null;

        private void Update(double deltaTime)
        {
            var dalt = this.instrumentData.altitude.vertical_speed /* fpm */ / 60.0 * deltaTime /* s */;
            this.instrumentData.altitude.altitude += (float)dalt;
            if (this.instrumentData.altitude.altitude > 10000.0 || this.instrumentData.altitude.altitude < 0.0)
            {
                this.instrumentData.altitude.vertical_speed *= -1;
            }
            this.InstrumentDataReceived(this, this.instrumentData);
        }

        public DummySimClient()
        {
            this.instrumentData.altitude.vertical_speed = 300;
            this.instrumentData.airspeed.kias = 200;
            this.instrumentData.airspeed.ktas = 300;
        }

        public void Close()
        {
            if (timer != null)
            {
                timer.Dispose();
                timer = null;
            }
        }

        public void Open(string name)
        {
            if (timer == null)
            {
                var last = DateTime.Now;
                const long interval = 1000;
                timer = new Timer((x) => {
                    var now = DateTime.Now;
                    this.Update((now - last).TotalSeconds);
                    last = now;
                    }, this, 0, interval);
            }
        }

        public void Open(string name, string hostName, int port, bool localIfAble = true)
        {
            this.Open(name);
        }
    }
}