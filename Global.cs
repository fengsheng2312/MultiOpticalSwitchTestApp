using OpticalMeter.Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Free_spaceOpticalCirculatorTestApp.ComunicationTransport;

namespace Free_spaceOpticalCirculatorTestApp
{
    public static class Global
    {
        public static Dictionary<string, IProtocol> protocols = new Dictionary<string, IProtocol>();
        public static Dictionary<string, IPowerMeterPlugin> plugins = new Dictionary<string, IPowerMeterPlugin>();
        public static IPowerMeter opticalPowermeter;

        public static String readChannel = "1";

        public static double Tx_comZeroVal = 0;

        public static double Com_rxZeroVal = 0;
    }
}
