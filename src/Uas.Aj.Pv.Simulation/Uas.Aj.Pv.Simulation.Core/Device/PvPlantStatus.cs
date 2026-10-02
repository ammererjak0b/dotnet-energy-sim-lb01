namespace Uas.Aj.Pv.Simulation.Core.Device;

// operating state of the plant
public enum PvPlantStatus
{
    Off, // switched off manually
    Night, // on, no irradiance
    Producing, // on, running free
    Limited, // on, capped by power limit
    Fault // inverter fault
}
