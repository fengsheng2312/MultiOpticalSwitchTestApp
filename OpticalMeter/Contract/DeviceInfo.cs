namespace OpticalMeter.Contract
{
    public class DeviceInfo
    {
        public string Vendor { get; }
        public string Model { get; }
        public string SerialNumber { get; }
        public string FirmwareVersion { get; }

        public DeviceInfo(string vendor, string model, string serialNumber, string firmwareVersion)
        {
            Vendor = vendor;
            Model = model;
            SerialNumber = serialNumber;
            FirmwareVersion = firmwareVersion;
        }

        public override string ToString() => $"{Vendor} {Model} (S/N: {SerialNumber})";
    }
}