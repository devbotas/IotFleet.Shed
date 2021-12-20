using System;
using DevBot9.Protocols.Homie;
using DevBot9.Protocols.Homie.Utilities;
using NLog;

namespace IotFleet.Shed;

partial class Domekt200 {
    private HostDevice _device;
    private PahoHostDeviceConnection _broker = new PahoHostDeviceConnection();
    private ReliableModbus _reliableModbus = new ReliableModbus();

    public HostChoiceProperty ActualState { get; private set; }
    public HostChoiceProperty ActualVentilationLevelProperty { get; private set; }
    public HostNumberProperty SupplyAirTemperatureProperty { get; private set; }

    HostTextProperty _actualDateTimeProperty;
    HostChoiceProperty _targetState;
    HostChoiceProperty _actualModbusConnectionState;
    HostNumberProperty _disconnectCount;

    private DateTime _startTime = DateTime.Now;
    private HostNumberProperty _systemUptime;

    public static Logger Log = LogManager.GetCurrentClassLogger();

    public Domekt200() { }
}
