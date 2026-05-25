using Newtonsoft.Json.Linq;
using StatsClient.MVVM.Core;
using StatsClient.MVVM.View;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StatsClient.MVVM.ViewModel;

public class FileRenameViewModel : ObservableObject
{
    #region Properties
    private static FileRenameViewModel? instance;
    public static FileRenameViewModel Instance
    {
        get => instance!;
        set
        {
            instance = value;
            RaisePropertyChangedStatic(nameof(Instance));
        }
    }


    private string progressText = "";
    public string ProgressText
    {
        get => progressText;
        set
        {
            progressText = value;
            RaisePropertyChanged(nameof(ProgressText));
        }
    }

    private string originalFilePath = "";
    public string OriginalFilePath
    {
        get => originalFilePath;
        set
        {
            originalFilePath = value;
            RaisePropertyChanged(nameof(OriginalFilePath));
            GetFileNameAndExtension(value);
        }
    }


    private string originalFileName = "";
    public string OriginalFileName
    {
        get => originalFileName;
        set
        {
            originalFileName = value;
            RaisePropertyChanged(nameof(OriginalFileName));
        }
    }

    private string selectedFile = "";
    public string SelectedFile
    {
        get => selectedFile;
        set
        {
            selectedFile = value;
            RaisePropertyChanged(nameof(SelectedFile));
        }
    }

    private string originalFileExtension = "";
    public string OriginalFileExtension
    {
        get => originalFileExtension;
        set
        {
            originalFileExtension = value;
            RaisePropertyChanged(nameof(OriginalFileExtension));
        }
    }

    private List<string> fileListsInFolder = [];
    public List<string> FileListsInFolder
    {
        get => fileListsInFolder;
        set
        {
            fileListsInFolder = value;
            RaisePropertyChanged(nameof(FileListsInFolder));
        }
    }
    #endregion

    public RelayCommand CloseWindowCommand { get; set; }
    public RelayCommand RenameCommand { get; set; }

    public FileRenameViewModel()
    {
        Instance = this;

        CloseWindowCommand = new RelayCommand(o => CloseWindow());
        RenameCommand = new RelayCommand(o => RenameAll(o));


    }

    private async Task LookForOtherFilesInFolder()
    {
        try
        {
            FileListsInFolder.Clear();

            string path = Path.GetDirectoryName(OriginalFilePath);

            DirectoryInfo d = new(path);

            FileInfo[] Files = d.GetFiles("*.stl");
            foreach (FileInfo file in Files)
                FileListsInFolder.Add(file.Name);

            FileInfo[] Files2 = d.GetFiles("*.dcm");
            foreach (FileInfo file in Files2)
                FileListsInFolder.Add(file.Name);

            ProgressText = $"";
        }
        catch { }
    }

    private async void GetFileNameAndExtension(string path)
    {
        OriginalFileName = Path.GetFileName(path);
        OriginalFileExtension = Path.GetExtension(path);
        await Task.Run(LookForOtherFilesInFolder);
        SelectedFile = OriginalFileName;
        FileRenameWindow.StaticInstance.listbox.Focus();
    }
    private void RenameAll(object obj)
    {
        string option = (string)obj;

        switch (option)
        {
            case "antagonist": RenameAllToAntagonist(); break;
            case "preparation": RenameAllToPreparation(); break;
            case "switch": SwitchRawPrepToAbutmentAlignment(); break;
        }
    }

    private async void SwitchRawPrepToAbutmentAlignment()
    {
        string path = Path.GetDirectoryName(OriginalFilePath);
        bool foundPrePreparationScan = false;
        bool foundRawPreparationScan = false;


        foreach (string file in FileListsInFolder)
        {
            if (file.StartsWith("Raw Preparation scan"))
                foundRawPreparationScan = true;

            if (file.StartsWith("PrePreparationScan"))
                foundPrePreparationScan = true;
        }

        if (foundRawPreparationScan && foundPrePreparationScan)
        {
            foreach (string file in FileListsInFolder)
            {
                if (!file.StartsWith('-') && File.Exists(Path.Combine(path, file)) && !File.Exists(Path.Combine(path, "-", file)))
                {
                    if (file.StartsWith("Raw Preparation scan") || file.StartsWith("PrePreparationScan"))
                    {
                        string newfilename = $"-{file}";
                        ProgressText = $"Renaming: {file} to {newfilename}";
                        try
                        {
                            File.Move(Path.Combine(path, file), Path.Combine(path, newfilename));
                        }
                        catch { }
                    }
                }
            }

            await Task.Run(LookForOtherFilesInFolder);

            foreach (string file in FileListsInFolder)
            {
                if (file.StartsWith('-') && File.Exists(Path.Combine(path, file)))
                {
                    if (file.StartsWith("-Raw Preparation scan"))
                    {
                        string newfilename = $"AbutmentAlignmentScan{Path.GetExtension(file)}";
                        ProgressText = $"Renaming: {file} to {newfilename}";
                        try
                        {
                            File.Move(Path.Combine(path, file), Path.Combine(path, newfilename));
                        }
                        catch { }
                    }
                    if (file.StartsWith("-PrePreparationScan"))
                    {
                        string newfilename = $"Raw Preparation scan{Path.GetExtension(file)}";
                        ProgressText = $"Renaming: {file} to {newfilename}";
                        try
                        {
                            File.Move(Path.Combine(path, file), Path.Combine(path, newfilename));
                        }
                        catch { }
                    }
                }
            }
        }

        await Task.Run(LookForOtherFilesInFolder);

        ShowSuccessMessage();
    }

    private void ShowSuccessMessage()
    {
        MainViewModel.Instance.ShowMessageBox("Success", "Files been successfully renamed!", Enums.SMessageBoxButtons.Ok, MainViewModel.NotificationIcon.Success, 5, FileRenameWindow.StaticInstance);
    }

    private async Task RenameAllFilesAddDashAtTheBeginingOfFileName()
    {
        string path = Path.GetDirectoryName(OriginalFilePath);

        foreach (string file in FileListsInFolder)
        {
            if (!file.StartsWith('-') && File.Exists(Path.Combine(path, file)) && !File.Exists(Path.Combine(path, "-", file)))
            {
                string newfilename = $"-{file}";
                ProgressText = $"Renaming: {file} to {newfilename}";
                try
                {
                    File.Move(Path.Combine(path, file), Path.Combine(path, newfilename));
                }
                catch { }
            }
        }

        await Task.Run(LookForOtherFilesInFolder);
    }

    private async Task RenameAllFilesRemoveDashFromTheBeginingOfFileName()
    {
        string path = Path.GetDirectoryName(OriginalFilePath);

        foreach (string file in FileListsInFolder)
        {
            if (file.StartsWith('-') && File.Exists(Path.Combine(path, file)) && !File.Exists(Path.Combine(path, "-", file)))
            {
                string newfilename = file.Substring(1);
                ProgressText = $"Renaming: {file} to {newfilename}";
                try
                {
                    File.Move(Path.Combine(path, file), Path.Combine(path, newfilename));
                }
                catch { }
            }
        }

        await Task.Run(LookForOtherFilesInFolder);
    }

    private async void RenameAllToPreparation()
    {
        await RenameAllFilesAddDashAtTheBeginingOfFileName();
        string path = Path.GetDirectoryName(OriginalFilePath);

        foreach (string file in FileListsInFolder)
        {
            string extension = Path.GetExtension(file);

            if (file.Equals($"-AntagonistScan{extension}") && File.Exists(Path.Combine(path, file)))
            {
                string newfilename = $"PreparationScan{extension}";
                if (!File.Exists(Path.Combine(path, newfilename)))
                {
                    ProgressText = $"Renaming: {file} to {newfilename}";
                    try
                    {
                        File.Move(Path.Combine(path, file), Path.Combine(path, newfilename));
                    }
                    catch { }
                }
            }
            if (file.Equals($"-MB Antagonist scan{extension}") && File.Exists(Path.Combine(path, file)))
            {
                string newfilename = $"MB Preparation scan{extension}";
                if (!File.Exists(Path.Combine(path, newfilename)))
                {
                    ProgressText = $"Renaming: {file} to {newfilename}";
                    try
                    {
                        File.Move(Path.Combine(path, file), Path.Combine(path, newfilename));
                    }
                    catch { }
                }
            }
            if (file.Equals($"-Raw Antagonist scan{extension}") && File.Exists(Path.Combine(path, file)))
            {
                string newfilename = $"Raw Preparation scan{extension}";
                if (!File.Exists(Path.Combine(path, newfilename)))
                {
                    ProgressText = $"Renaming: {file} to {newfilename}";
                    try
                    {
                        File.Move(Path.Combine(path, file), Path.Combine(path, newfilename));
                    }
                    catch { }
                }
            }
        }

        ProgressText = $"Renaming finished!";
        await RenameAllFilesRemoveDashFromTheBeginingOfFileName();
        await Task.Delay(500);
        await Task.Run(LookForOtherFilesInFolder);
        ShowSuccessMessage();
    }
    private async void RenameAllToAntagonist()
    {
        await RenameAllFilesAddDashAtTheBeginingOfFileName();
        string path = Path.GetDirectoryName(OriginalFilePath);

        foreach (string file in FileListsInFolder)
        {
            string extension = Path.GetExtension(file);

            if (file.Equals($"-PreparationScan{extension}") && File.Exists(Path.Combine(path, file)))
            {
                string newfilename = $"AntagonistScan{extension}";
                if (!File.Exists(Path.Combine(path, newfilename)))
                {
                    ProgressText = $"Renaming: {file} to {newfilename}";
                    try
                    {
                        File.Move(Path.Combine(path, file), Path.Combine(path, newfilename));
                    }
                    catch { }
                }
            }
            if (file.Equals($"-MB Preparation scan{extension}") && File.Exists(Path.Combine(path, file)))
            {
                string newfilename = $"MB Antagonist scan{extension}";
                if (!File.Exists(Path.Combine(path, newfilename)))
                {
                    ProgressText = $"Renaming: {file} to {newfilename}";
                    try
                    {
                        File.Move(Path.Combine(path, file), Path.Combine(path, newfilename));
                    }
                    catch { }
                }
            }
            if (file.Equals($"-Raw Preparation scan{extension}") && File.Exists(Path.Combine(path, file)))
            {
                string newfilename = $"Raw Antagonist scan{extension}";
                if (!File.Exists(Path.Combine(path, newfilename)))
                {
                    ProgressText = $"Renaming: {file} to {newfilename}";
                    try
                    {
                        File.Move(Path.Combine(path, file), Path.Combine(path, newfilename));
                    }
                    catch { }
                }
            }
        }

        ProgressText = $"Renaming finished!";
        await RenameAllFilesRemoveDashFromTheBeginingOfFileName();
        await Task.Delay(500);
        await Task.Run(LookForOtherFilesInFolder);
        ShowSuccessMessage();
    }



    private void CloseWindow()
    {
        FileRenameWindow.StaticInstance.Close();
    }
}
