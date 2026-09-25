using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Desktop
{
    public sealed class MyComputerView : DesktopAppView
    {
        [Serializable] private sealed class DriveRow
        {
            public GameObject root;
            public TMP_Text name;
            public TMP_Text capacity;
            public Image usage;
        }
        [SerializeField] private DriveRow[] drives;
        [SerializeField] private GameObject empty;
        protected override void Refresh()
        {
            empty.SetActive(State.Storage.Drives.Count == 0);
            for (int i = 0; i < drives.Length; i++)
            {
                bool exists = i < State.Storage.Drives.Count;
                drives[i].root.SetActive(exists);
                if (!exists) continue;
                DesktopDrive drive = State.Storage.Drives[i];
                drives[i].name.text = F("desktop.disk.name", drive.Letter);
                drives[i].capacity.text = F("desktop.disk.capacity", State.Storage.GetFreeMiB(drive.DriveId) / 1024f, drive.CapacityMiB / 1024f);
                float fraction = drive.CapacityMiB > 0 ? (float)State.Storage.GetUsedMiB(drive.DriveId) / drive.CapacityMiB : 0;
                drives[i].usage.rectTransform.anchorMax = new Vector2(fraction, 1);
            }
        }
    }
}
