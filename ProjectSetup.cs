using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using static System.IO.Path;
using static UnityEditor.AssetDatabase;
using Directory = System.IO.Directory;

public static class ProjectSetup
{
    private static string folderPath;
    
    [MenuItem("Tools/Project Setup/Create Folders")]
    public static void CreateFolders()
    {
        Folders.Create("_Project", "Animation", "Art", "Materials", "Prefabs",
            "Scripts", "Scripts/Managers",
            "Scripts/Controllers", "Scripts/Models", "Scripts/Views",
            "Scriptables", "Scripts/Utilities");

        Refresh();

        Folders.Move("_Project", "Scenes");
        // Folders.Delete("Scenes");

        Refresh();
    }
    
    [MenuItem("Tools/Project Setup/Import Essential Assets")]
    public static void ImportEssentialsAssets()
    {
        AssetLoader.LoadFolderPath();
        if (string.IsNullOrEmpty(folderPath))
        {
            Debug.LogError($"folder path to load assets from is not selected. Use Tools => Select Folder Path to select folder first.");
            return;
        }
        
        Assets.ImportAssets();
    }

    [MenuItem("Tools/Project Setup/Import Essential Scripts")]
    public static void ImportEssentialScripts()
    {
        AssetLoader.LoadFolderPath();
        if (string.IsNullOrEmpty(folderPath))
        {
            Debug.LogError($"folder path to load scripts from is not selected. Use Tools => Select Folder Path to select folder first.");
            return;
        }
        
        Scripts.ImportScript();
    }

    [MenuItem("Tools/Project Setup/Install Essential Packages")]
    public static void InstallBuiltInPackages()
    {
        Packages.InstallPackages(new[]
        {
            "com.unity.textmeshpro",
            "com.unity.jobs",
            "com.unity.addressables",
            "com.unity.nuget.newtonsoft-json",
        });
    }

    public class AssetLoader : EditorWindow
    {
        private string selectedPath = string.Empty;
        
        [MenuItem("Tools/Project Setup/Select Folder Path")]
        public static void ShowWindow()
        {
            var window = GetWindow<AssetLoader>("Select Asset Folder");
            window.LoadSavedFolderPath();
        }
        
        private void LoadSavedFolderPath()
        {
            selectedPath = EditorPrefs.GetString("SelectedAssetFolderPath", string.Empty);
        }
        
        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(selectedPath) || !string.IsNullOrEmpty(folderPath))
            {
                if (!string.IsNullOrEmpty(folderPath))
                {
                    GUILayout.Label("Selected Folder: " + folderPath, EditorStyles.wordWrappedLabel);                    
                }
                else if(!string.IsNullOrEmpty(selectedPath))
                {
                    GUILayout.Label("Selected Folder: " + selectedPath, EditorStyles.wordWrappedLabel);   
                }
            }
            
            if (GUILayout.Button("Select Folder Path"))
            {
                selectedPath = EditorUtility.OpenFolderPanel("Select Folder", "", "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    Debug.Log("Selected Folder: " + selectedPath);
                    
                    Repaint();
                }
            }
            
            if (GUILayout.Button("Remove Folder Path"))
            {
                selectedPath = string.Empty;
                EditorPrefs.DeleteKey("SelectedAssetFolderPath");
            }
            
            // Pushes the Save button to the bottom
            GUILayout.FlexibleSpace();

            if (!string.IsNullOrEmpty(selectedPath))
            {
                if (GUILayout.Button("Save"))
                {
                    folderPath = selectedPath;
                    EditorPrefs.SetString("SelectedAssetFolderPath", folderPath);
                    Debug.Log($"folder path saved");
                    
                    this.Close();
                }
            }
        }
        
        public static void LoadFolderPath()
        {
            folderPath = EditorPrefs.GetString("SelectedAssetFolderPath", string.Empty);
        }
    }

    static class Folders
    {
        public static void Create(string root, params string[] folders)
        {
            var fullpath = Combine(Application.dataPath, root);
            if (!Directory.Exists(fullpath))
            {
                Directory.CreateDirectory(fullpath);
            }

            foreach (var folder in folders)
            {
                CreateSubFolders(fullpath, folder);
            }
        }

        static void CreateSubFolders(string rootPath, string folderHierarchy)
        {
            var folders = folderHierarchy.Split('/');
            var currentPath = rootPath;

            foreach (var folder in folders)
            {
                currentPath = Combine(currentPath, folder);
                if (!Directory.Exists(currentPath))
                {
                    Directory.CreateDirectory(currentPath);
                }
            }
        }

        public static void Move(string newParent, string folderName)
        {
            var sourcePath = $"Assets/{folderName}";
            if (IsValidFolder(sourcePath))
            {
                var destinationPath = $"Assets/{newParent}/{folderName}";
                var error = MoveAsset(sourcePath, destinationPath);

                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"Failed to move {folderName}: {error}");
                }
            }
        }

        public static void Delete(string folderName)
        {
            var pathToDelete = $"Assets/{folderName}";

            if (IsValidFolder(pathToDelete))
            {
                DeleteAsset(pathToDelete);
            }
        }
    }

    static class Assets
    {
        public static void ImportAssets()
        {
            // Get all unity package files in the source folder
            string[] assetFiles = Directory.GetFiles(folderPath, "*.unitypackage", SearchOption.AllDirectories);
            
            foreach (string assetFile in assetFiles)
            {
                ImportPackage(Combine(folderPath, assetFile), false);
            
                Refresh();
            }
        }
    }

    static class Scripts
    {
        public static void ImportScript()
        {
            string targetFolder = Combine("Assets", "_Project", "Scripts", "Utilities");

            if (!IsValidFolder(targetFolder))
            {
                CreateFolder(Combine("Assets", "_Project", "Scripts"), "Utilities");
            }

            // Get all script files in the source folder
            string[] scriptFiles = Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories);

            foreach (string scriptFile in scriptFiles)
            {
                string fileName = GetFileName(scriptFile);
                string targetPath = Combine(targetFolder, fileName);

                File.Copy(scriptFile, targetPath, true);

                ImportAsset(targetPath);
            }

            Refresh();
        }
    }

    static class Packages
    {
        static AddRequest request;
        static Queue<string> packagesToInstall = new Queue<string>();

        public static void InstallPackages(string[] packages)
        {
            foreach (var package in packages)
            {
                packagesToInstall.Enqueue(package);
            }

            if (packagesToInstall.Count > 0)
            {
                StartNextPackageInstallation();
            }
        }

        static async void StartNextPackageInstallation()
        {
            request = Client.Add(packagesToInstall.Dequeue());

            while (!request.IsCompleted) await Task.Delay(10);

            if (request.Status == StatusCode.Success) Debug.Log("Installed: " + request.Result.packageId);
            else if (request.Status >= StatusCode.Failure) Debug.LogError(request.Error.message);

            if (packagesToInstall.Count > 0)
            {
                await Task.Delay(1000);
                StartNextPackageInstallation();
            }
        }
    }
}