﻿using System.Runtime.InteropServices;

namespace FFXIVClientStructs.FFXIV.Component.GUI
{
    [StructLayout(LayoutKind.Explicit, Size = 0x18)]
    public unsafe struct AtkUldComponentInfo
    {
        [FieldOffset(0x0)] public AtkUldObjectInfo ObjectInfo;
        [FieldOffset(0x10)] public ComponentType ComponentType;
    }
}