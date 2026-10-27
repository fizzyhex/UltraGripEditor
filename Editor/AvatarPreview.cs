using System;
using System.IO;
using Cysharp.Threading.Tasks;
using SLZ.Marrow.Warehouse;
using UnityEditor;
using UnityEngine;

namespace UltraGripEditor
{
    [InitializeOnLoad]
    public static class AvatarPreview
    {
        private const string BarcodePref = "UltraGripEditor.PreviewAvatar";
        private const string ModsPathPref = "UltraGripEditor.ModsPath";

        public static string Barcode => EditorPrefs.GetString(BarcodePref, "");
        public static string Status { get; private set; } = "";
        public static event Action StatusChanged;

        private static int loadId;

        // BONELAB's persistentDataPath/Mods
        public static string ModsPath
        {
            get
            {
                string custom = EditorPrefs.GetString(ModsPathPref, "");
                if(!string.IsNullOrEmpty(custom)) return custom;
                string localLow = Directory.GetParent(Application.persistentDataPath).Parent.FullName;
                return Path.Combine(localLow, "Stress Level Zero", "BONELAB", "Mods");
            }
        }

        static AvatarPreview()
        {
            AssetWarehouse.OnReady(HookWarehouse);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if(Barcode != "") EditorApplication.delayCall += () => Load(Barcode).Forget();
        }

        // Deferred because the SDK reinitialises UniTask's player loop on EnteredEditMode, dropping tasks queued before it
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if(Barcode == "") return;
            if(state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += () => Load(Barcode).Forget();
        }

        // Clearing the warehouse unloads the avatar's bundle and destroys the loaded asset
        private static void HookWarehouse()
        {
            if(AssetWarehouse.Instance == null) return;
            AssetWarehouse.Instance.OnClearEditor -= OnWarehouseCleared;
            AssetWarehouse.Instance.OnClearEditor += OnWarehouseCleared;
        }

        private static void OnWarehouseCleared()
        {
            if(Barcode != "") Load(Barcode).Forget();
        }

        public static void Set(string barcode)
        {
            barcode = barcode.Trim();
            if(barcode == Barcode && GripVisualizer.PreviewAvatar != null) return;
            EditorPrefs.SetString(BarcodePref, barcode);
            Load(barcode).Forget();
        }

        private static async UniTaskVoid Load(string barcode)
        {
            int id = ++loadId;
            GripVisualizer.SetPreviewAvatar(null);
            if(barcode == "")
            {
                SetStatus("");
                return;
            }

            // The editor's AssetWarehouse is null in play mode
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SetStatus("Unavailable in play mode");
                return;
            }

            SetStatus("Loading...");
            QueueEditorUpdateLoop.StartEditorUpdateLoop();
            GameObject avatar = null;
            string title = null;
            string error = null;
            try
            {
                (avatar, title) = await LoadAvatar(new Barcode(barcode));
            }
            catch(Exception e)
            {
                error = e.Message;
                Debug.LogException(e);
            }
            finally
            {
                QueueEditorUpdateLoop.StopEditorUpdateLoop();
            }
            if(id != loadId) return;

            if(avatar == null)
            {
                SetStatus(error ?? "Avatar not found");
                return;
            }

            Animator animator = avatar.GetComponentInChildren<Animator>(true);
            if(animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                SetStatus("Not a humanoid avatar");
                return;
            }

            GripVisualizer.SetPreviewAvatar(avatar);
            SetStatus(title);
        }

        private static async UniTask<(GameObject, string)> LoadAvatar(Barcode barcode)
        {
            await UniTask.WaitUntil(() => AssetWarehouse.ready && AssetWarehouse.Instance != null);
            HookWarehouse();

            if(!AssetWarehouse.Instance.TryGetCrate(barcode, out AvatarCrate crate))
            {
                string palletPath = FindModPallet(barcode.ID);
                if(palletPath == null) return (null, null);
                SetStatus("Loading pallet...");
                await AssetWarehouse.Instance.LoadPalletFromFolderAsync(palletPath);
                if(!AssetWarehouse.Instance.TryGetCrate(barcode, out crate)) return (null, null);
            }

            if(crate.MainAsset.EditorAsset is GameObject local) return (local, crate.Title);
            return (await crate.LoadAssetAsync(), crate.Title);
        }

        private static string FindModPallet(string barcode)
        {
            if(!Directory.Exists(ModsPath)) return null;
            string quoted = $"\"{barcode}\"";
            foreach(string dir in Directory.GetDirectories(ModsPath))
            {
                foreach(string path in Directory.GetFiles(dir, "*.pallet.json"))
                {
                    if(File.ReadAllText(path).Contains(quoted)) return path;
                }
            }
            return null;
        }

        private static void SetStatus(string status)
        {
            Status = status;
            StatusChanged?.Invoke();
        }
    }
}
