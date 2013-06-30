// Copyright 2013 Joshua R. Rodgers
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ========================================================================

using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using SharpDX;
using SharpDX.D3DCompiler;

namespace ShaderCompiler
{
    public class Shader : Task
    {
        [Required]
        public ITaskItem[] InputFiles
        {
            get;
            set;
        }

        [Required]
        public string OutputPath
        {
            get; 
            set;
        }

        [Required]
        public string Profile
        {
            get;
            set;
        }

        [Output]
        public ITaskItem[] OutputFiles
        {
            get;
            set;
        }

        public string EntryPoint
        {
            get;
            set;
        }

        public string IncludePath
        {
            get; 
            set;
        }

        public bool IsDebug 
        { 
            get;
            set; 
        }

        public int? OptimiizationLevel
        {
            get;
            set;
        }

        public override bool Execute()
        {
            OutputFiles = (from shaderInput in InputFiles
                           where !string.IsNullOrWhiteSpace(shaderInput.ItemSpec)
                           select shaderInput.ItemSpec
                           into inputFile 
                           let outputFile = Path.Combine(OutputPath, Path.GetFileNameWithoutExtension(inputFile) + ".cso")
                           where outputFile != null
                           select CompileShaderFile(inputFile, outputFile)).ToArray();

            return !Log.HasLoggedErrors;
        }

        private ITaskItem CompileShaderFile(string sourceFile, string outputFile)
        {
            try
            {
                var shaderFlags = GetShaderFlags();

                using (var shaderInclude = new ShaderInclude(IncludePath ?? string.Empty))
                using (var compilerResult = ShaderBytecode.CompileFromFile(sourceFile, EntryPoint ?? "main", Profile, include: shaderInclude, shaderFlags: shaderFlags))
                {
                    if (compilerResult.HasErrors)
                    {
                        int line;
                        int column;
                        string errorMessage;

                        GetCompileResult(compilerResult.Message, out line, out column, out errorMessage);

                        Log.LogError("Shader", compilerResult.ResultCode.ToString(), string.Empty, sourceFile, line, column, 0, 0, errorMessage);
                    }

                    var fileInfo = new FileInfo(outputFile);
                    fileInfo.Directory.Create();
                    
                    File.WriteAllBytes(outputFile, compilerResult.Bytecode.Data);
                    return new TaskItem(outputFile);
                }
            }
            catch (CompilationException ex)
            {
                int line;
                int column;
                string errorMessage;

                GetCompileResult(ex.Message, out line, out column, out errorMessage);

                Log.LogError("Shader", ex.ResultCode.ToString(), string.Empty, sourceFile, line, column, 0, 0, errorMessage);
                return null;
            }
            catch (Exception ex)
            {
                Log.LogError("Shader",
                             ex.HResult.ToString(CultureInfo.InvariantCulture),
                             string.Empty,
                             sourceFile,
                             0,
                             0,
                             0,
                             0,
                             string.Format("Critical failure ({0}:) {1}", ex.GetType(), ex.Message));
                return null;
            }
        }

        private ShaderFlags GetShaderFlags()
        {
            if (IsDebug)
            {
                return ShaderFlags.Debug | ShaderFlags.SkipOptimization;
            }

            switch (OptimiizationLevel)
            {
                case 0:
                    return ShaderFlags.OptimizationLevel0;
                case 1:
                    return ShaderFlags.OptimizationLevel1;
                case 2:
                    return ShaderFlags.OptimizationLevel2;
                case 3:
                    return ShaderFlags.OptimizationLevel3;
                default:
                    return ShaderFlags.OptimizationLevel1;
            }
        }

        private void GetCompileResult(string error, out int line, out int column, out string errorMessage)
        {
            var lineColumnMatch = Regex.Match(error, @"^.*\((?<line>\d+),(?<col>\d+)\):");

            line = int.Parse(lineColumnMatch.Groups["line"].Captures[0].Value);
            column = int.Parse(lineColumnMatch.Groups["col"].Captures[0].Value);

            var errorMessageMatch = Regex.Match(error, @"error .*: (?<error>.*)$");
            errorMessage = errorMessageMatch.Groups["error"].Value;
        }
    }
}
