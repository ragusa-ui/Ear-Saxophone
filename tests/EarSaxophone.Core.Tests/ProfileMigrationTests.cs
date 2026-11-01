using System.Text.Json.Nodes;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.Persistence;

namespace EarSaxophone.Core.Tests;

/// <summary>Profile file format version 2 and the migration of version 1 files (written by rc2 and earlier).</summary>
public class ProfileMigrationTests : IDisposable
{
    /// <summary>Exactly what version 1 of the program wrote (generated with the rc2 code, including the computed properties it used to write).</summary>
    internal const string RealV1File = """
        {
          "schemaVersion": 1,
          "activeProfile": "Streaming",
          "profiles": [
            {
              "name": "Streaming",
              "mappings": [
                {
                  "control": {
                    "kind": "controlChange",
                    "channel": 0,
                    "number": 0
                  },
                  "action": {
                    "type": "setVolume",
                    "target": {
                      "kind": "application",
                      "flow": "render",
                      "role": "console",
                      "deviceId": null,
                      "app": {
                        "exeName": "Discord.exe",
                        "exePath": null,
                        "isValid": true
                      },
                      "isValid": true
                    },
                    "isContinuous": true,
                    "isValid": true
                  },
                  "scaling": null,
                  "effectiveScaling": {
                    "min": 0,
                    "max": 1,
                    "invert": false,
                    "isValid": true
                  }
                },
                {
                  "control": {
                    "kind": "controlChange",
                    "channel": 0,
                    "number": 1
                  },
                  "action": {
                    "type": "setVolume",
                    "target": {
                      "kind": "defaultDevice",
                      "flow": "capture",
                      "role": "console",
                      "deviceId": null,
                      "app": null,
                      "isValid": true
                    },
                    "isContinuous": true,
                    "isValid": true
                  },
                  "scaling": {
                    "min": 0.1,
                    "max": 0.9,
                    "invert": true,
                    "isValid": true
                  },
                  "effectiveScaling": {
                    "min": 0.1,
                    "max": 0.9,
                    "invert": true,
                    "isValid": true
                  }
                },
                {
                  "control": {
                    "kind": "controlChange",
                    "channel": 0,
                    "number": 48
                  },
                  "action": {
                    "type": "toggleMute",
                    "target": {
                      "kind": "defaultDevice",
                      "flow": "capture",
                      "role": "communications",
                      "deviceId": null,
                      "app": null,
                      "isValid": true
                    },
                    "isContinuous": false,
                    "isValid": true
                  },
                  "scaling": null,
                  "effectiveScaling": {
                    "min": 0,
                    "max": 1,
                    "invert": false,
                    "isValid": true
                  }
                },
                {
                  "control": {
                    "kind": "controlChange",
                    "channel": 0,
                    "number": 49
                  },
                  "action": {
                    "type": "toggleMute",
                    "target": {
                      "kind": "application",
                      "flow": "render",
                      "role": "console",
                      "deviceId": null,
                      "app": {
                        "exeName": "Discord.exe",
                        "exePath": null,
                        "isValid": true
                      },
                      "isValid": true
                    },
                    "isContinuous": false,
                    "isValid": true
                  },
                  "scaling": null,
                  "effectiveScaling": {
                    "min": 0,
                    "max": 1,
                    "invert": false,
                    "isValid": true
                  }
                },
                {
                  "control": {
                    "kind": "controlChange",
                    "channel": 0,
                    "number": 41
                  },
                  "action": {
                    "type": "media",
                    "command": "playPause",
                    "isContinuous": false,
                    "isValid": true
                  },
                  "scaling": null,
                  "effectiveScaling": {
                    "min": 0,
                    "max": 1,
                    "invert": false,
                    "isValid": true
                  }
                },
                {
                  "control": {
                    "kind": "note",
                    "channel": 1,
                    "number": 60
                  },
                  "action": {
                    "type": "hotkey",
                    "keys": "Ctrl\u002BAlt\u002BM",
                    "isContinuous": false,
                    "isValid": true
                  },
                  "scaling": null,
                  "effectiveScaling": {
                    "min": 0,
                    "max": 1,
                    "invert": false,
                    "isValid": true
                  }
                },
                {
                  "control": {
                    "kind": "controlChange",
                    "channel": 0,
                    "number": 58
                  },
                  "action": {
                    "type": "switchDevice",
                    "flow": "render",
                    "deviceIds": [
                      "{0.0.0.00000000}.{a}",
                      "{0.0.0.00000000}.{b}"
                    ],
                    "isContinuous": false,
                    "isValid": true
                  },
                  "scaling": null,
                  "effectiveScaling": {
                    "min": 0,
                    "max": 1,
                    "invert": false,
                    "isValid": true
                  }
                },
                {
                  "control": {
                    "kind": "controlChange",
                    "channel": 0,
                    "number": 59
                  },
                  "action": {
                    "type": "switchDevice",
                    "flow": "capture",
                    "deviceIds": null,
                    "isContinuous": false,
                    "isValid": true
                  },
                  "scaling": null,
                  "effectiveScaling": {
                    "min": 0,
                    "max": 1,
                    "invert": false,
                    "isValid": true
                  }
                }
              ]
            },
            {
              "name": "Leer",
              "mappings": []
            }
          ]
        }
        """;

    /// <summary>A hand-written version 1 file with the minimum of fields.</summary>
    private const string MinimalV1File = """
        {
          "schemaVersion": 1,
          "activeProfile": "Spiel",
          "profiles": [
            {
              "name": "Spiel",
              "mappings": [
                { "control": { "kind": "controlChange", "channel": 0, "number": 32 },
                  "action": { "type": "toggleMute", "target": { "kind": "defaultDevice", "flow": "render" } } },
                { "control": { "kind": "controlChange", "channel": 0, "number": 2 },
                  "action": { "type": "setVolume", "target": { "kind": "application", "app": { "exeName": "game.exe", "exePath": "C:\\Games\\game.exe" } } },
                  "scaling": { "min": 0.25, "max": 0.75, "invert": false } },
                { "control": { "kind": "controlChange", "channel": 0, "number": 33 }, "action": null }
              ]
            }
          ]
        }
        """;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "earsax-migr-" + Guid.NewGuid().ToString("N"));

    public ProfileMigrationTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static ControlId Cc(int n) => new(ControlKind.ControlChange, 0, (byte)n);

    /// <summary>What <see cref="RealV1File"/> contains, in the version 2 model.</summary>
    private static Profile ExpectedStreaming() => new("Streaming",
    [
        new ControlMapping(Cc(0), new SetVolumeAction(AudioTarget.Application("Discord.exe"))),
        new ControlMapping(Cc(1), new SetVolumeAction(AudioTarget.Microphone), new ValueScaling(0.1f, 0.9f, true)),
        new ControlMapping(Cc(48), new MuteAction(new AudioTarget(AudioTargetKind.DefaultDevice, DeviceFlow.Capture, DeviceRole.Communications), MuteMode.Toggle)),
        new ControlMapping(Cc(49), new MuteAction(AudioTarget.Application("Discord.exe"))),
        new ControlMapping(Cc(41), new MediaAction(MediaCommand.PlayPause)),
        new ControlMapping(new ControlId(ControlKind.Note, 1, 60), new HotkeyAction("Ctrl+Alt+M")),
        new ControlMapping(Cc(58), new SwitchDeviceAction(DeviceFlow.Render, ["{0.0.0.00000000}.{a}", "{0.0.0.00000000}.{b}"])),
        new ControlMapping(Cc(59), new SwitchDeviceAction(DeviceFlow.Capture)),
    ]);

    private static ProfileDocument V2Sample() => new(ProfileDocument.CurrentSchemaVersion, "Spiele",
    [
        new Profile("Spiele",
        [
            new ControlMapping(Cc(3),
                [new SetVolumeAction(AudioTarget.Application("game1.exe")), new SetVolumeAction(AudioTarget.Application("game2.exe"))],
                new ValueScaling(0f, 0.8f, false), "Spiele"),
            new ControlMapping(Cc(48), [new MuteAction(AudioTarget.Microphone), new MuteAction(AudioTarget.Application("Discord.exe"))]),
            new ControlMapping(Cc(64), new MuteAction(AudioTarget.Microphone, MuteMode.UnmuteWhileHeld)),
            new ControlMapping(Cc(65), new MuteAction(AudioTarget.Speakers, MuteMode.MuteWhileHeld), Name: "Ruhe"),
            new ControlMapping(Cc(41), [new MediaAction(MediaCommand.PlayPause), new HotkeyAction("Ctrl+Shift+P")]),
            new ControlMapping(Cc(58), new SwitchDeviceAction(DeviceFlow.Capture, ["m1"])),
        ]),
        new Profile("Leer", []),
    ]);

    [Fact]
    public void RealV1File_IsMigratedWithoutLoss()
    {
        ProfileReadResult read = ProfileSerializer.Read(RealV1File);

        Assert.Equal(1, read.SourceSchemaVersion);
        Assert.True(read.WasMigrated);
        Assert.Equal(ProfileDocument.CurrentSchemaVersion, read.Document.SchemaVersion);
        Assert.Equal("Streaming", read.Document.ActiveProfile);
        Assert.Equal(["Streaming", "Leer"], read.Document.Profiles.Select(p => p.Name));
        Assert.Equal(ExpectedStreaming().Mappings, read.Document.Profiles[0].Mappings);
        Assert.Empty(read.Document.Profiles[1].Mappings);
        Assert.All(read.Document.Profiles[0].Mappings, m => Assert.Single(m.Actions));
        Assert.Empty(read.Document.Profiles.SelectMany(p => p.Validate()));
    }

    [Fact]
    public void OldToggleMute_BecomesMuteInToggleMode()
    {
        ProfileDocument document = ProfileSerializer.Deserialize(RealV1File);

        var mute = Assert.IsType<MuteAction>(document.Profiles[0].Find(Cc(48))!.Actions[0]);
        Assert.Equal(MuteMode.Toggle, mute.Mode);
        Assert.Equal(DeviceFlow.Capture, mute.Target.Flow);
        Assert.Equal(DeviceRole.Communications, mute.Target.Role);
    }

    [Fact]
    public void MinimalV1File_KeepsScalingAndPath_AndANullActionBecomesAnEmptyMapping()
    {
        ProfileDocument document = ProfileSerializer.Deserialize(MinimalV1File);
        Profile profile = document.Profiles[0];

        Assert.Equal(new MuteAction(AudioTarget.Speakers), Assert.Single(profile.Find(Cc(32))!.Actions));
        ControlMapping game = profile.Find(Cc(2))!;
        Assert.Equal(new SetVolumeAction(new AudioTarget(AudioTargetKind.Application, App: new AppSelector("game.exe", @"C:\Games\game.exe"))), Assert.Single(game.Actions));
        Assert.Equal(new ValueScaling(0.25f, 0.75f, false), game.Scaling);
        Assert.Empty(profile.Find(Cc(33))!.Actions);
        Assert.Contains(profile.Validate(), p => p.Contains("keine Aktion", StringComparison.Ordinal));
    }

    [Fact]
    public void MigratedFile_IsWrittenAsVersion2_WithActionLists_AndReadsBackEqual()
    {
        ProfileDocument migrated = ProfileSerializer.Deserialize(RealV1File);
        string json = ProfileSerializer.Serialize(migrated);
        JsonObject root = JsonNode.Parse(json)!.AsObject();

        Assert.Equal(2, root["schemaVersion"]!.GetValue<int>());
        JsonObject first = root["profiles"]![0]!["mappings"]![0]!.AsObject();
        Assert.False(first.ContainsKey("action"));
        Assert.IsType<JsonArray>(first["actions"]);
        Assert.DoesNotContain("toggleMute", json);
        Assert.Contains("\"type\": \"mute\"", json);
        Assert.Contains("\"mode\": \"toggle\"", json);

        ProfileReadResult again = ProfileSerializer.Read(json);
        Assert.False(again.WasMigrated);
        Assert.Equal(ExpectedStreaming().Mappings, again.Document.Profiles[0].Mappings);
    }

    [Fact]
    public void V2_RoundTripsMultipleActions_ModesAndNames()
    {
        ProfileDocument original = V2Sample();
        ProfileReadResult copy = ProfileSerializer.Read(ProfileSerializer.Serialize(original));

        Assert.False(copy.WasMigrated);
        Assert.Equal(original.ActiveProfile, copy.Document.ActiveProfile);
        Assert.Equal(original.Profiles.Count, copy.Document.Profiles.Count);
        for (int i = 0; i < original.Profiles.Count; i++)
        {
            Assert.Equal(original.Profiles[i].Name, copy.Document.Profiles[i].Name);
            Assert.Equal(original.Profiles[i].Mappings, copy.Document.Profiles[i].Mappings);
        }
    }

    [Fact]
    public void V2_WritesReadableModes_AndNoComputedProperties()
    {
        string json = ProfileSerializer.Serialize(V2Sample());

        Assert.Contains("\"mode\": \"unmuteWhileHeld\"", json);
        Assert.Contains("\"mode\": \"muteWhileHeld\"", json);
        Assert.Contains("\"name\": \"Spiele\"", json);
        Assert.DoesNotContain("isValid", json);
        Assert.DoesNotContain("isContinuous", json);
        Assert.DoesNotContain("effectiveScaling", json);
        Assert.DoesNotContain("selectedIds", json);
    }

    [Fact]
    public void V2_ToleratesTypeNotFirst_MissingMode_AndOldTypeName()
    {
        const string json = """
            { "schemaVersion": 2, "activeProfile": "p", "profiles": [ { "name": "p", "mappings": [
              { "control": { "kind": "controlChange", "channel": 0, "number": 32 },
                "actions": [ { "target": { "kind": "defaultDevice", "flow": "capture" }, "type": "mute" },
                             { "type": "toggleMute", "target": { "kind": "defaultDevice" } } ] } ] } ] }
            """;

        ControlMapping mapping = ProfileSerializer.Deserialize(json).Profiles[0].Mappings[0];

        Assert.Equal([new MuteAction(AudioTarget.Microphone), new MuteAction(AudioTarget.Speakers)], mapping.Actions);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":3,\"activeProfile\":\"x\",\"profiles\":[]}", true)]
    [InlineData("{\"schemaVersion\":99,\"activeProfile\":\"x\",\"profiles\":[{\"name\":\"x\",\"mappings\":[{\"whatever\":{}}]}]}", true)]
    [InlineData("{\"activeProfile\":\"x\",\"profiles\":[]}", true)]
    [InlineData("[]", false)]
    [InlineData("{\"schemaVersion\":\"2\",\"activeProfile\":\"x\",\"profiles\":[]}", false)]
    [InlineData("{\"schemaVersion\":2,\"activeProfile\":\"x\"}", false)]
    [InlineData("{\"schemaVersion\":2,\"activeProfile\":\"x\",\"profiles\":[{\"name\":\"x\",\"mappings\":[{\"control\":{},\"actions\":[null]}]}]}", false)]
    [InlineData("{\"schemaVersion\":2,\"activeProfile\":\"x\",\"profiles\":[{\"name\":\"x\",\"mappings\":[{\"control\":{},\"actions\":[{\"type\":\"unknownThing\"}]}]}]}", false)]
    public void Read_RefusesNewerVersionsAndBrokenFiles(string json, bool isVersionProblem)
    {
        var ex = Assert.Throws<ProfileFormatException>(() => ProfileSerializer.Read(json));
        Assert.Equal(isVersionProblem, ex.Message.StartsWith("Nicht unterstützte Version", StringComparison.Ordinal));
    }

    [Fact]
    public void Store_LoadsV1_AndKeepsAnUntouchedBackupBeforeTheFirstOverwrite()
    {
        string path = Path.Combine(_dir, "profiles.json");
        File.WriteAllText(path, RealV1File);
        var store = new ProfileStore(path);

        ProfileLoadResult loaded = store.Load();
        Assert.Null(loaded.Warning);
        Assert.Equal(1, loaded.MigratedFrom);
        Assert.Equal(path + ".v1.bak", store.V1BackupPath);
        Assert.False(File.Exists(store.V1BackupPath));          // loading alone changes nothing on disk
        Assert.Equal(RealV1File, File.ReadAllText(path));

        store.Save(loaded.Document);

        Assert.Equal(RealV1File, File.ReadAllText(store.V1BackupPath));
        Assert.Equal(2, ProfileSerializer.PeekSchemaVersion(File.ReadAllText(path)));
        ProfileLoadResult reloaded = store.Load();
        Assert.Null(reloaded.MigratedFrom);
        Assert.Equal(ExpectedStreaming().Mappings, reloaded.Document.Profiles[0].Mappings);
    }

    [Fact]
    public void Store_CreatesTheBackupOnlyOnce_AndNeverOverwritesIt()
    {
        string path = Path.Combine(_dir, "profiles.json");
        File.WriteAllText(path, RealV1File);
        var store = new ProfileStore(path);
        store.Save(store.Load().Document);
        store.Save(V2Sample());
        Assert.Equal(RealV1File, File.ReadAllText(store.V1BackupPath));

        // Someone puts another version 1 file in place: the first backup stays as it was.
        File.WriteAllText(path, MinimalV1File);
        store.Save(store.Load().Document);
        Assert.Equal(RealV1File, File.ReadAllText(store.V1BackupPath));
        Assert.Equal(2, ProfileSerializer.PeekSchemaVersion(File.ReadAllText(path)));
    }

    [Fact]
    public void Store_WritesNoBackup_ForNewFilesAndVersion2Files()
    {
        string path = Path.Combine(_dir, "profiles.json");
        var store = new ProfileStore(path);

        store.Save(V2Sample());
        store.Save(V2Sample());

        Assert.False(File.Exists(store.V1BackupPath));
        Assert.Equal(V2Sample().Profiles[0].Mappings, store.Load().Document.Profiles[0].Mappings);
    }

    [Fact]
    public void Store_NewerVersion_IsRefused_AndKeptAsCorrupt()
    {
        string path = Path.Combine(_dir, "profiles.json");
        string newer = RealV1File.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 3", StringComparison.Ordinal);
        File.WriteAllText(path, newer);

        ProfileLoadResult result = new ProfileStore(path).Load();

        Assert.NotNull(result.Warning);
        Assert.Contains("Version 3", result.Warning);
        Assert.Equal(newer, File.ReadAllText(path + ".corrupt"));
        Assert.False(File.Exists(path + ".v1.bak"));
    }
}
