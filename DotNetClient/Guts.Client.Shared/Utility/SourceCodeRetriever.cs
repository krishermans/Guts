﻿using System;
using System.Text;
using Guts.Client.Shared.TestTools;

namespace Guts.Client.Shared.Utility
{
    public static class SourceCodeRetriever
    {
        public static string ReadSourceCodeFiles(string sourceCodeRelativeFilePaths)
        {
            if (string.IsNullOrEmpty(sourceCodeRelativeFilePaths)) return null;

            var paths = sourceCodeRelativeFilePaths.Split(" ,;".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            var sourceCodeBuilder = new StringBuilder();
            foreach (var path in paths)
            {
                var trimmedPath = path.Trim('\n', '\r');
                sourceCodeBuilder.AppendLine($"///{trimmedPath}///");
                sourceCodeBuilder.AppendLine();
                sourceCodeBuilder.Append(Solution.Current.GetFileContent(trimmedPath));
                sourceCodeBuilder.AppendLine();
            }

            return sourceCodeBuilder.ToString();
        }
    }
}