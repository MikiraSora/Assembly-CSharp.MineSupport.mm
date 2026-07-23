using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            System.Console.Error.WriteLine("Usage: MineSupport.OfflineValidator <target.dll> <output.dll> <patch1.mm.dll> [patch2.mm.dll ...]");
            return 1;
        }

        try
        {
            var target = Path.GetFullPath(args[0]);
            var output = Path.GetFullPath(args[1]);
            var patches = args.Skip(2).Select(Path.GetFullPath).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            using (var modder = new MonoModder
            {
                InputPath = target,
                OutputPath = output,
                LogVerboseEnabled = false
            })
            {
                // IL 合并验证只关心目标程序集，不让旧版 Cecil/PDB writer 的局部变量范围阻断输出。
                modder.ReaderParameters.ReadSymbols = false;
                modder.WriterParameters.WriteSymbols = false;
                modder.Read();
                for (var i = 0; i < patches.Length; i++)
                    modder.ReadMod(patches[i]);

                AddDependencyDir(modder, Path.GetDirectoryName(target));
                AddDependencyDir(modder, Path.GetDirectoryName(output));
                for (var i = 0; i < patches.Length; i++)
                {
                    AddDependencyDir(modder, Path.GetDirectoryName(patches[i]));
                    AddSiblingCoreDir(modder, Path.GetDirectoryName(patches[i]));
                }
                AddSiblingCoreDir(modder, Path.GetDirectoryName(target));

                modder.MapDependencies();
                modder.AutoPatch();
                using (var stream = File.Create(output))
                    modder.Write(stream, output);
            }

            using (var module = ModuleDefinition.ReadModule(output))
                Validate(module);

            System.Console.WriteLine("MineSupport.OfflineValidator: PASS");
            return 0;
        }
        catch (Exception exception)
        {
            System.Console.Error.WriteLine("MineSupport.OfflineValidator: FAIL");
            PrintException(exception, string.Empty);
            return 2;
        }
    }

    private static void Validate(ModuleDefinition module)
    {
        Require(module.GetType("MonoMod.WasHere") != null, "MonoMod.WasHere is missing");
        Require(module.Types.All(type => !type.Name.StartsWith("patch_", StringComparison.Ordinal)), "patch_* type remains in output");
        var monoModReference = module.AssemblyReferences.FirstOrDefault(reference => reference.Name == "MonoMod");
        if (monoModReference != null)
            System.Console.WriteLine("[Validator] note: unused MonoMod AssemblyRef retained by Cecil; no Mine type/method references are emitted");

        RequireType(module, "MineSupport.MineRuntime");
        RequireType(module, "MineSupport.MineVisual");
        RequireType(module, "MineSupport.PatchLog");
        var mineTailParser = RequireType(module, "MineSupport.MineTailParser");
        var mineChartLoader = RequireType(module, "MineSupport.MineChartLoader");
        Require(AnyMethodCalls(mineTailParser, "TryParse", "String", "Contains")
                && AnyMethodCalls(mineTailParser, "TryParse", "String", "Replace"),
            "MineTailParser does not use exact String.Contains/Replace marker handling");
        Require(AnyMethodCalls(mineChartLoader, "TryPrepare", "String", "Contains"),
            "MineChartLoader does not use String.Contains for Mine detection");

        var noteData = RequireType(module, "Manager.NoteData");
        Require(noteData.Fields.Any(field => field.Name == "isMine"), "NoteData.isMine is missing");
        Require(noteData.Fields.Any(field => field.Name == "mineRecordOrdinal"), "NoteData.mineRecordOrdinal is missing");
        var clearChain = noteData.Methods.Where(method => method.Name.IndexOf("clear", StringComparison.Ordinal) >= 0).ToArray();
        Require(clearChain.Any(method => WritesField(method, "isMine"))
                && clearChain.Any(method => WritesField(method, "mineRecordOrdinal"))
                && clearChain.Any(method => WritesField(method, "mineRawTail")),
            "NoteData.clear patch chain does not reset Mine metadata");

        var notesReader = RequireType(module, "Manager.NotesReader");
        Require(AnyMethodCalls(notesReader, "loadMa2Main", "MineChartLoader", "TryPrepare"), "loadMa2Main does not validate Mine tails");
        Require(AnyMethodCalls(notesReader, "loadMa2Main", "MineVisual", "EnsureAvailable"), "loadMa2Main does not validate Mine resources");
        Require(AnyMethodCalls(notesReader, "loadMa2Main", "MineChartLoader", "TryNormalizeSlideChains"), "loadMa2Main does not validate Slide chains");
        Require(AnyMethodCalls(notesReader, "calcEach", null, "orig_calcEach"), "calcEach wrapper does not call the original Each builder");
        Require(AnyMethodCalls(notesReader, "loadNote", null, "set_Item"), "loadNote does not sanitize the MA2 tail before original parsing");
        Require(AllMethods(notesReader).Any(method => Calls(method, null, "__SoflanLoadNote")), "Soflan loadNote hook is missing from the Mine patch chain");
        Require(AllMethods(notesReader).Any(method => Calls(method, null, "__SoflanClearAll")), "Soflan loadMa2Main clear hook is missing");
        Require(AllMethods(notesReader).Any(method => Calls(method, null, "__SoflanLoadComposition")), "Soflan composition hook is missing");

        var soflanMarkerParser = RequireType(module, "SoflanSupport.SoflanMarkerParser");
        Require(AnyMethodCalls(soflanMarkerParser, "TryParse", "Regex", "Matches"),
            "SoflanMarkerParser does not use Regex.Matches for mixed modifier fields");
        var soflanManager = RequireType(module, "SoflanSupport.SoflanManager");
        Require(AnyMethodCalls(soflanManager, "loadNote", "SoflanMarkerParser", "TryParse"),
            "SoflanManager.loadNote does not use the shared regex marker parser");
        Require(AnyMethodCalls(soflanManager, "FailSoflanMarker", "PatchLog", "Error"),
            "Soflan marker failures do not use the Release error log entry point");

        var gameManager = RequireType(module, "Manager.GameManager");
        var isAutoPlay = gameManager.Methods.First(method => method.Name == "IsAutoPlay" && method.Parameters.Count == 0);
        Require(Calls(isAutoPlay, "MineRuntime", "get_SuppressAutoPlay"), "GameManager.IsAutoPlay is not Mine-context aware");

        var gamePlayManager = RequireType(module, "Manager.GamePlayManager");
        Require(AnyMethodCalls(gamePlayManager, "Initialize", "MineRuntime", "ClearTransientState"),
            "GamePlayManager.Initialize does not clear Mine transient state");

        var gameScore = RequireType(module, "Manager.GameScoreList");
        Require(AnyMethodCalls(gameScore, "SetResult", "MineRuntime", "TryConvertResult"), "GameScoreList.SetResult does not apply Mine policy");
        Require(AnyMethodCalls(gameScore, "SetResult", "MineRuntime", "RecordResult"), "GameScoreList.SetResult does not record final outcomes");
        Require(AnyMethodCalls(gameScore, "FinishPlay", "MineRuntime", "EnterNaturalFinish"), "FinishPlay is not marked as a natural result source");

        var mineRuntime = RequireType(module, "MineSupport.MineRuntime");
        Require(!AllMethods(mineRuntime).Any(method => Calls(method, "GameManager", "set_AutoPlay")), "MineRuntime still mutates global GameManager.AutoPlay");
        var clearTransientState = mineRuntime.Methods.First(method => method.Name == "ClearTransientState");
        Require(WritesField(clearTransientState, "feedbackSuppressionDepth")
                && WritesField(clearTransientState, "autoPlaySuppressionDepth")
                && WritesField(clearTransientState, "resultSource")
                && WritesField(clearTransientState, "resultScore")
                && WritesField(clearTransientState, "resultIndex")
                && WritesField(clearTransientState, "resultRuntimeObject"),
            "MineRuntime.ClearTransientState does not reset the complete thread-local result context");

        var mineVisual = RequireType(module, "MineSupport.MineVisual");
        Require(AnyMethodCalls(mineVisual, "EnsureAvailable", "AssetBundle", "LoadFromFile"), "MineVisual does not load the AssetBundle");
        Require(AnyMethodCalls(mineVisual, "Apply", null, "GetComponentsInChildren"), "MineVisual does not cache child renderers on first apply");
        Require(AnyMethodCalls(mineVisual, "Clear", null, "Restore"), "MineVisual does not restore cached renderer state");
        Require(AnyMethodCalls(mineVisual, "Apply", "GameObject", "SetActive")
                && AnyMethodCalls(mineVisual, "Restore", "GameObject", "SetActive"),
            "MineVisual does not suppress and restore the EX overlay active state");

        var slideRoot = RequireType(module, "Monitor.SlideRoot");
        Require(AnyMethodCalls(slideRoot, "UpdateBreakEffect", "MineRuntime", "IsMine"),
            "SlideRoot.UpdateBreakEffect does not suppress Mine break flashing");

        var hotTypes = new[]
        {
            "Monitor.NoteBase",
            "Monitor.HoldNote",
            "Monitor.BreakHoldNote",
            "Monitor.BreakNote",
            "Monitor.TouchNoteB",
            "Monitor.TouchHoldC",
            "Monitor.SlideRoot",
            "Monitor.SlideFan"
        };
        for (var i = 0; i < hotTypes.Length; i++)
        {
            var type = RequireType(module, hotTypes[i]);
            foreach (var method in type.Methods.Where(method => method.Name == "NoteCheck"))
                Require(!CreatesDelegate(method), hotTypes[i] + ".NoteCheck allocates a delegate");
        }
    }

    private static TypeDefinition RequireType(ModuleDefinition module, string fullName)
    {
        var type = module.GetType(fullName);
        Require(type != null, fullName + " is missing");
        return type;
    }

    private static IEnumerable<MethodDefinition> AllMethods(TypeDefinition type)
    {
        return type.Methods.Concat(type.NestedTypes.SelectMany(AllMethods));
    }

    private static bool AnyMethodCalls(TypeDefinition type, string methodName, string declaringTypeName, string calledName)
    {
        return AllMethods(type).Any(method => method.Name == methodName && Calls(method, declaringTypeName, calledName));
    }

    private static bool Calls(MethodDefinition method, string declaringTypeName, string calledName)
    {
        if (method?.Body == null)
            return false;

        return method.Body.Instructions.Any(instruction =>
        {
            if (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt)
                return false;
            var called = instruction.Operand as MethodReference;
            if (called == null || called.Name != calledName)
                return false;
            return declaringTypeName == null || called.DeclaringType.Name == declaringTypeName;
        });
    }

    private static bool CreatesDelegate(MethodDefinition method)
    {
        if (method?.Body == null)
            return false;

        return method.Body.Instructions.Any(instruction =>
            instruction.OpCode == OpCodes.Newobj
            && instruction.Operand is MethodReference constructor
            && (constructor.DeclaringType.FullName.StartsWith("System.Action", StringComparison.Ordinal)
                || constructor.DeclaringType.FullName.StartsWith("System.Func", StringComparison.Ordinal)));
    }

    private static bool WritesField(MethodDefinition method, string fieldName)
    {
        if (method?.Body == null)
            return false;

        return method.Body.Instructions.Any(instruction =>
            (instruction.OpCode == OpCodes.Stfld || instruction.OpCode == OpCodes.Stsfld)
            && instruction.Operand is FieldReference field
            && field.Name == fieldName);
    }

    private static void AddDependencyDir(MonoModder modder, string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return;

        var fullPath = Path.GetFullPath(directory);
        if (modder.DependencyDirs == null)
            modder.DependencyDirs = new List<string>();
        if (!modder.DependencyDirs.Any(item => string.Equals(Path.GetFullPath(item), fullPath, StringComparison.OrdinalIgnoreCase)))
            modder.DependencyDirs.Add(fullPath);

        var resolver = modder.AssemblyResolver as BaseAssemblyResolver;
        if (resolver != null && !resolver.GetSearchDirectories().Any(item =>
            string.Equals(Path.GetFullPath(item), fullPath, StringComparison.OrdinalIgnoreCase)))
        {
            resolver.AddSearchDirectory(fullPath);
        }
    }

    private static void AddSiblingCoreDir(MonoModder modder, string directory)
    {
        if (string.IsNullOrEmpty(directory))
            return;

        var parent = Directory.GetParent(Path.GetFullPath(directory));
        if (parent == null)
            return;

        var core = Path.Combine(parent.FullName, "core");
        if (Directory.Exists(core))
            AddDependencyDir(modder, core);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void PrintException(Exception exception, string indent)
    {
        if (exception == null)
            return;

        System.Console.Error.WriteLine(indent + exception.GetType().FullName + ": " + exception.Message);
        System.Console.Error.WriteLine(indent + exception.StackTrace);
        PrintException(exception.InnerException, indent + "  ");
    }
}
