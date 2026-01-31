namespace Modules;

public class StatData
{
    public long Timestamp { get; set; }
    public CpuStat Cpu { get; set; } = new();
    public MemoryStat Memory { get; set; } = new();
    public GpuStat Gpu { get; set; } = new();
    public DiskStat Disk { get; set; } = new();
    public NetworkStat Network { get; set; } = new();
}

public class CpuStat { public double Usage { get; set; } }
public class MemoryStat { public double Load { get; set; } }
public class GpuStat { public int SocketPower { get; set; } public int CorePower { get; set; } }
public class DiskStat { public long Read_bytes_per_sec { get; set; } public long Write_bytes_per_sec { get; set; } }
public class NetworkStat { public long Bytes_sent_per_sec { get; set; } public long Bytes_received_per_sec { get; set; } }
