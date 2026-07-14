using System.Runtime.InteropServices;

namespace HdrPilot.Native;

/// <summary>
/// Rohe P/Invoke-Signaturen und Strukturen für die Windows DisplayConfig-API (user32.dll).
/// Referenz: wingdi.h / winuser.h (Windows SDK 10.0.26100).
///
/// Es gibt zwei Wege, HDR ("Advanced Color") zu schalten:
///   1) Legacy (Win11 vor 24H2): DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE (10)
///   2) Modern (Win11 24H2+):    DISPLAYCONFIG_SET_HDR_STATE (16) + GET_ADVANCED_COLOR_INFO_2 (15)
/// <see cref="HdrPilot.Core.HdrController"/> wählt zur Laufzeit den passenden Weg.
/// </summary>
internal static class DisplayConfig
{
    // ---- QueryDisplayConfig Flags ----
    internal const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;

    // ---- DISPLAYCONFIG_DEVICE_INFO_TYPE (Werte laut wingdi.h) ----
    internal const int DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
    internal const int DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO = 9;    // Legacy get
    internal const int DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE = 10;  // Legacy set
    internal const int DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2 = 15; // 24H2 get
    internal const int DISPLAYCONFIG_DEVICE_INFO_SET_HDR_STATE = 16;             // 24H2 set

    // ---- DISPLAYCONFIG_ADVANCED_COLOR_MODE (24H2) ----
    internal const uint DISPLAYCONFIG_ADVANCED_COLOR_MODE_SDR = 0;
    internal const uint DISPLAYCONFIG_ADVANCED_COLOR_MODE_WCG = 1; // "Auto Color Management" (ACM)
    internal const uint DISPLAYCONFIG_ADVANCED_COLOR_MODE_HDR = 2;

    // ---- Mode-Info ----
    internal const uint DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE = 1;
    internal const uint DISPLAYCONFIG_PATH_MODE_IDX_INVALID = 0xFFFFFFFF;

    internal const int ERROR_SUCCESS = 0;

    [DllImport("user32.dll")]
    internal static extern int GetDisplayConfigBufferSizes(
        uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    internal static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    internal static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME deviceName);

    [DllImport("user32.dll")]
    internal static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO colorInfo);

    [DllImport("user32.dll")]
    internal static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 colorInfo2);

    [DllImport("user32.dll")]
    internal static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE setState);

    [DllImport("user32.dll")]
    internal static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SET_HDR_STATE setHdr);

    // ============ Strukturen ============

    [StructLayout(LayoutKind.Sequential)]
    internal struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public int type;
        public int size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public DISPLAYCONFIG_RATIONAL refreshRate;
        public uint scanLineOrdering;
        public int targetAvailable; // BOOL
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINTL
    {
        public int x;
        public int y;
    }

    /// <summary>
    /// Quellmodus eines Pfads. Die Desktop-Position (position) verrät den Primärmonitor:
    /// dessen Quellmodus liegt immer bei (0,0).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_SOURCE_MODE
    {
        public uint width;
        public uint height;
        public uint pixelFormat;
        public POINTL position;
    }

    // DISPLAYCONFIG_MODE_INFO ist eine feste 64-Byte-Struktur:
    //   infoType (4) + id (4) + adapterId/LUID (8) + Union (48, größte Variante
    //   DISPLAYCONFIG_TARGET_MODE mit DISPLAYCONFIG_VIDEO_SIGNAL_INFO) = 64.
    // Wir interpretieren nur die Source-Variante (für die Primärmonitor-Erkennung);
    // Size = 64 sorgt dafür, dass QueryDisplayConfig das Array korrekt befüllt.
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DISPLAYCONFIG_MODE_INFO
    {
        [FieldOffset(0)] public uint infoType;   // 1 = Source, 2 = Target
        [FieldOffset(4)] public uint id;
        [FieldOffset(8)] public LUID adapterId;
        [FieldOffset(16)] public DISPLAYCONFIG_SOURCE_MODE sourceMode; // gültig bei infoType == 1
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint flags;
        public uint outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string monitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string monitorDevicePath;
    }

    // ---- Legacy Advanced Color (vor 24H2) ----
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value; // Bitfeld, siehe Properties
        public uint colorEncoding;
        public int bitsPerColorChannel;

        public bool AdvancedColorSupported => (value & 0x1) != 0;      // Bit 0
        public bool AdvancedColorEnabled => (value & 0x2) != 0;        // Bit 1
        public bool AdvancedColorForceDisabled => (value & 0x8) != 0;  // Bit 3
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value; // Bit 0 = enableAdvancedColor
    }

    // ---- Modern (24H2+) ----
    // Feldreihenfolge laut SDK: header, value, colorEncoding, bitsPerColorChannel,
    // activeColorMode (empirisch verifiziert: activeColorMode wechselt beim
    // HDR-Umschalten zwischen 1/WCG und 2/HDR).
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value;
        // Bitfeld "value":
        //   Bit 0 advancedColorSupported     Bit 1 advancedColorActive
        //   Bit 2 reserved                   Bit 3 advancedColorLimitedByPolicy
        //   Bit 4 highDynamicRangeSupported  Bit 5 highDynamicRangeUserEnabled
        //   Bit 6 wideColorSupported         Bit 7 wideColorUserEnabled
        public uint colorEncoding;
        public int bitsPerColorChannel;
        public uint activeColorMode; // DISPLAYCONFIG_ADVANCED_COLOR_MODE: 0=SDR, 1=WCG/ACM, 2=HDR

        public bool AdvancedColorSupported => (value & 0x01) != 0;
        public bool AdvancedColorActive => (value & 0x02) != 0;
        public bool AdvancedColorLimitedByPolicy => (value & 0x08) != 0;
        public bool HdrSupported => (value & 0x10) != 0;
        public bool HdrUserEnabled => (value & 0x20) != 0;

        /// <summary>True, wenn Windows "Auto Color Management" (ACM) auf diesem Monitor aktiv ist.</summary>
        public bool AcmActive =>
            AdvancedColorActive && activeColorMode == DISPLAYCONFIG_ADVANCED_COLOR_MODE_WCG;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_SET_HDR_STATE
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value; // Bit 0 = enableHdr
    }
}
