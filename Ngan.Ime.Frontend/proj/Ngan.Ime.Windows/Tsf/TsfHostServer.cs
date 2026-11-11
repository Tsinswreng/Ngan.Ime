namespace Ngan.Ime.Windows.Tsf;

using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Ngan.Ime.Core.Keys;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// TSF DLL 的宿主端。按鍵經由具名管道送進來，交給 Rime（ISvcIme）處理，
/// 再把 preedit／候選／commit 回給 TSF；候選窗由本行程顯示並跟隨游標。
/// </summary>
public static class TsfHostServer {
    public const string PipeName = "NganIme.Tsf.v1";
    static readonly Dictionary<string, IKeyChar> KeyByName = BuildKeyMap();
    static TsfCandidateWindow? Window;

    public static void Start() {
        _ = Task.Run(LoopAsync);
    }

    static Dictionary<string, IKeyChar> BuildKeyMap() {
        var Map = new Dictionary<string, IKeyChar>(StringComparer.Ordinal);
        foreach (var Prop in typeof(KeyChars).GetProperties(BindingFlags.Public | BindingFlags.Static)) {
            if (Prop.GetValue(null) is IKeyChar Char && !string.IsNullOrEmpty(Char.Name)) {
                Map[Char.Name] = Char;
            }
        }
        return Map;
    }

    static readonly SemaphoreSlim RimeLock = new(1, 1);

    static async Task LoopAsync() {
        // 允許同時多個連線（explorer、notepad 各自持有一條），Rime 由 RimeLock 串行化。
        while (true) {
            var Server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 16, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            try {
                await Server.WaitForConnectionAsync();
                _ = Task.Run(() => ServeAsync(Server));
            }
            catch (Exception Ex) {
                AppLog.Error(Ex, "[TsfHost] accept");
                Server.Dispose();
                await Task.Delay(50);
            }
        }
    }

    static async Task ServeAsync(NamedPipeServerStream Server) {
        using (Server) {
            try {
                using var Reader = new StreamReader(Server, Encoding.UTF8, leaveOpen: true);
                using var Writer = new StreamWriter(Server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                var Line = await Reader.ReadLineAsync();
                if (Line is null) {
                    return;
                }
                await RimeLock.WaitAsync();
                string Reply;
                try {
                    Reply = await HandleAsync(Line);
                }
                finally {
                    RimeLock.Release();
                }
                await Writer.WriteLineAsync(Reply);
                await Writer.FlushAsync();
                Server.WaitForPipeDrain();
            }
            catch (Exception Ex) {
                AppLog.Error(Ex, "[TsfHost] serve");
            }
        }
    }

    static async Task<string> HandleAsync(string Line) {
        var Parts = Line.Split('|');
        if (Parts.Length < 5 || Parts[0] != "K") {
            return "R|0|0||0|0";
        }
        var Vk = int.Parse(Parts[1]);
        var Mods = int.Parse(Parts[2]);
        var X = int.Parse(Parts[3]);
        var Y = int.Parse(Parts[4]);
        var Svc = Di.SvcProvider?.GetService<ISvcIme>();
        var Key = Resolve(Vk);
        if (Svc is null || Key is null) {
            HideWindow();
            return "R|0|0||0|0";
        }
        var Commits = new List<string>();
        void OnCommit(object? Sender, string Text) => Commits.Add(Text);
        Svc.OnCommit += OnCommit;
        try {
            var State = new KeyBoardState { AllDownKeys = new HashSet<IKeyChar>() };
            if ((Mods & 1) != 0) { State.AllDownKeys.Add(KeyChars.Shift_L); }
            if ((Mods & 2) != 0) { State.AllDownKeys.Add(KeyChars.Ctrl_L); }
            if ((Mods & 4) != 0) { State.AllDownKeys.Add(KeyChars.Alt_L); }
            var Event = new KeyEvent {
                KeyChar = Key,
                KeyState = KeyStates.Down,
                KeyBoardState = State,
            };
            await Svc.Input([Event], CancellationToken.None);
        }
        catch (Exception Ex) {
            AppLog.Error(Ex, "[TsfHost] input");
            return "R|0|0||0|0";
        }
        finally {
            Svc.OnCommit -= OnCommit;
        }
        var Preedit = Svc.Preedit ?? "";
        var Page = Svc.Candidates;
        var Eaten = Commits.Count > 0 || !string.IsNullOrEmpty(Preedit) || (Page?.Data?.Count ?? 0) > 0;
        PostUpdate(Preedit, Page, X, Y);
        return BuildReply(Eaten, Preedit, Page, Commits);
    }

    static IKeyChar? Resolve(int Vk) {
        string? Name = null;
        if (Vk is >= 0x41 and <= 0x5A) {
            Name = ((char)('a' + Vk - 0x41)).ToString();
        }
        else if (Vk is >= 0x30 and <= 0x39) {
            Name = ((char)('0' + Vk - 0x30)).ToString();
        }
        else if (Vk == 0x20) {
            Name = " ";
        }
        else if (Vk == 0x08) {
            Name = "Backspace";
        }
        else if (Vk == 0x0D) {
            Name = "Enter";
        }
        return Name is not null && KeyByName.TryGetValue(Name, out var Char) ? Char : null;
    }

    static string BuildReply(bool Eaten, string Preedit, CandidatePage? Page, List<string> Commits) {
        var Builder = new StringBuilder();
        Builder.Append("R|").Append(Eaten ? 1 : 0).Append('|')
            .Append(Page?.HighlightedIndex ?? 0).Append('|')
            .Append(B64(Preedit)).Append('|');
        var Items = Page?.Data ?? [];
        Builder.Append(Items.Count).Append('|');
        foreach (var Item in Items) {
            Builder.Append(B64(Item.Text)).Append('~').Append(B64(Item.Comment)).Append('|');
        }
        Builder.Append(Commits.Count).Append('|');
        foreach (var Text in Commits) {
            Builder.Append(B64(Text)).Append('|');
        }
        return Builder.ToString();
    }

    static string B64(string Text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(Text ?? ""));

    static void PostUpdate(string Preedit, CandidatePage? Page, int X, int Y) {
        Dispatcher.UIThread.Post(() => {
            Window ??= new TsfCandidateWindow();
            Window.Update(Preedit, Page, X, Y);
        });
    }

    static void HideWindow() {
        Dispatcher.UIThread.Post(() => Window?.HideIfVisible());
    }
}
