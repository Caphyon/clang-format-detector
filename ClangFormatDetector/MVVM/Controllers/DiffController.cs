﻿using ClangFormatDetector.DiffStyle;
using ClangFormatDetector.Enums;
using ClangFormatDetector.Helpers;
using ClangFormatDetector.Interfaces;
using ClangFormatDetector.MVVM.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace ClangFormatDetector.MVVM.Controllers
{
  public class DiffController
  {
    #region Members

    private readonly StyleDetector styleDetector;

    #endregion

    #region Constructor

    public DiffController()
    {
      styleDetector = new StyleDetector();
    }

    #endregion

    #region Properties 

    public CancellationTokenSource CancellationSource { get; set; }

    public bool CancelTokenDisposed { get; set; }

    #endregion

    #region Public Methods

    public void CloseLoadDetectionView(object sender, EventArgs e)
    {
      if (CancelTokenDisposed == false)
      {
        CancellationSource.Cancel();
      }
    }

    public async Task<(FormatStyle matchedStyle, List<IFormatOption> matchedOptions)> GetFormatOptionsAsync(List<string> filesContent, CancellationToken cancelToken)
    {
      return await styleDetector.DetectStyleOptionsAsync(filesContent, cancelToken);
    }

    public async Task<List<(FlowDocument, FlowDocument)>> CreateFlowDocumentsAsync(List<string> filesContent, FormatStyle formatStyle, List<IFormatOption> formatOptions, CancellationToken cancelToken)
    {
      var flowDocuments = new List<(FlowDocument, FlowDocument)>();

      foreach (var file in filesContent)
      {
        var documents = await CreateFlowDocumentAsync(file, formatStyle, formatOptions, cancelToken);
        flowDocuments.Add(documents);
      }
      return flowDocuments;
    }

    public async Task<(FlowDocument, FlowDocument)> CreateFlowDocumentAsync(string input, FormatStyle formatStyle, List<IFormatOption> formatOptions, CancellationToken cancelToken)
    {
      var diffMatchPatchWrapper = new DiffMatchPatchWrapper();
      string output = string.Empty;
      await Task.Run(() =>
      {
        var formatter = new StyleFormatter();
        output = formatter.FormatText(input, formatOptions, formatStyle);
        diffMatchPatchWrapper.Diff(input, output);
        diffMatchPatchWrapper.CleanupSemantic();
      }, cancelToken);
      return diffMatchPatchWrapper.DiffAsFlowDocuments(input, output);
    }

    public async Task<(bool, string)> CheckOptionValidityAsync(string input, FormatStyle formatStyle, List<IFormatOption> formatOptions)
    {
      string output = string.Empty;
      await Task.Run(() =>
      {
        var formatter = new StyleFormatter();
        output = formatter.FormatText(input, formatOptions, formatStyle);
      });

      if (output.Contains("YAML"))
      {
        return (true, output);
      }
      return (false, string.Empty);
    }


    public List<string> GetFileNames(List<string> filePaths)
    {
      var fileNames = new List<string>();
      foreach (var path in filePaths)
      {
        fileNames.Add(Path.GetFileName(path));
      }
      return fileNames;
    }

    public void CopyOptionValues(IFormatOption optionToChange, IFormatOption optionToCopy)
    {
      switch (optionToChange)
      {
        case FormatOptionToggleModel toggleModel:
          toggleModel.BooleanCombobox = ((FormatOptionToggleModel)optionToCopy).BooleanCombobox;
          break;
        case FormatOptionInputModel inputModel:
          inputModel.Input = ((FormatOptionInputModel)optionToCopy).Input;
          break;
        case FormatOptionMultipleToggleModel multipleToggleModel:
          var defaultMultipleToggle = (FormatOptionMultipleToggleModel)optionToCopy;
          for (int i = 0; i < multipleToggleModel.ToggleFlags.Count; i++)
          {
            multipleToggleModel.ToggleFlags[i].Value = defaultMultipleToggle.ToggleFlags[i].Value;
          }
          break;
        case FormatOptionMultipleInputModel multipleInputModel:
          multipleInputModel.MultipleInput = ((FormatOptionMultipleInputModel)optionToCopy).MultipleInput;
          break;
        default:
          break;
      }
    }

    public bool IsOptionChanged(IFormatOption option, IFormatOption defaultOption)
    {
      switch (option)
      {
        case FormatOptionToggleModel toggleModel:
          if (toggleModel.BooleanCombobox != ((FormatOptionToggleModel)defaultOption).BooleanCombobox)
          {
            return true;
          }
          break;
        case FormatOptionInputModel inputModel:
          if (inputModel.Input != ((FormatOptionInputModel)defaultOption).Input)
          {
            return true;
          }
          break;
        case FormatOptionMultipleToggleModel multipleToggleModel:
          var defaultMultipleToggle = (FormatOptionMultipleToggleModel)defaultOption;
          for (int i = 0; i < multipleToggleModel.ToggleFlags.Count; i++)
          {
            if (multipleToggleModel.ToggleFlags[i].Value != defaultMultipleToggle.ToggleFlags[i].Value)
            {
              return true;
            }
          }
          break;
        case FormatOptionMultipleInputModel multipleInputModel:
          if (multipleInputModel.MultipleInput != ((FormatOptionMultipleInputModel)defaultOption).MultipleInput)
          {
            return true;
          }
          break;
        default:
          break;
      }
      return false;
    }

    public void DeleteFormatFolder()
    {
      string folderPath = Path.Combine(Environment.CurrentDirectory, "Format");
      FileSystem.DeleteDirectory(folderPath);
    }

    #endregion
  }
}
