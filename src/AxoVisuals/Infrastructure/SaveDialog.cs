using System.Runtime.InteropServices;
namespace AxoVisuals.Infrastructure;

// The Common Item Dialog returns a path without creating or truncating the destination.
internal static class SaveDialog
{
    public static string? PickFolder(nint owner)
    {
        var instance = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7"))!)!;
        var dialog = (IFileDialog)instance;
        try
        {
            dialog.SetOptions(0x00000020 | 0x00000040 | 0x00000800 | 0x02000000 | 0x00000008);
            dialog.SetTitle("Choose output folder");
            int result = dialog.Show(owner);
            if (result == unchecked((int)0x800704C7)) return null;
            Marshal.ThrowExceptionForHR(result);
            dialog.GetResult(out var item);
            try
            {
                item.GetDisplayName(0x80058000, out var pointer);
                try { return Marshal.PtrToStringUni(pointer); }
                finally { Marshal.FreeCoTaskMem(pointer); }
            }
            finally { Marshal.FinalReleaseComObject(item); }
        }
        finally { Marshal.FinalReleaseComObject(instance); }
    }
    public static string? Pick(nint owner, string suggestedName, string format, string extension)
    {
        var instance = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B"))!)!;
        var dialog = (IFileDialog)instance;
        try
        {
            dialog.SetOptions(0x00000040 | 0x00000800 | 0x00010000 | 0x02000000 | 0x00000008);
            dialog.SetTitle("Export image");
            dialog.SetFileTypes(1,[new FilterSpec { Name = format, Specification = format == "JPEG" ? "*.jpg;*.jpeg" : "*" + extension }]);
            dialog.SetDefaultExtension(extension.TrimStart('.'));
            dialog.SetFileName(suggestedName);
            int result = dialog.Show(owner);
            if(result == unchecked((int)0x800704C7)) return null;
            Marshal.ThrowExceptionForHR(result);
            dialog.GetResult(out var item);
            try
            {
                item.GetDisplayName(0x80058000,out var pointer);
                try { return Marshal.PtrToStringUni(pointer); }
                finally { Marshal.FreeCoTaskMem(pointer); }
            }
            finally { Marshal.FinalReleaseComObject(item); }
        }
        finally { Marshal.FinalReleaseComObject(instance); }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FilterSpec
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string Name;
        [MarshalAs(UnmanagedType.LPWStr)] public string Specification;
    }
    [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        [PreserveSig] int Show(nint owner);
        void SetFileTypes(uint count,[MarshalAs(UnmanagedType.LPArray,SizeParamIndex=0)] FilterSpec[] filters);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(nint events,out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint options);
        void GetOptions(out uint options);
        void SetDefaultFolder(IShellItem folder);
        void SetFolder(IShellItem folder);
        void GetFolder(out IShellItem folder);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName(out nint name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetResult(out IShellItem item);
        void AddPlace(IShellItem item,uint placement);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        void Close(int result);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(nint filter);
    }
    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(nint context,ref Guid handler,ref Guid iid,out nint result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint displayName,out nint name);
        void GetAttributes(uint mask,out uint attributes);
        void Compare(IShellItem item,uint hint,out int order);
    }
}

