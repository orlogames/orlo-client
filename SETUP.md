# Orlo Client — Unity 6 Setup

## Quick Start

1. Install **Unity 6** (6000.x) via Unity Hub
2. Open this folder as a Unity project
3. Unity will generate the Library, Temp, and project files
4. Open `Assets/Scenes/Boot.unity` (create if needed)
5. Create an empty GameObject, attach `GameBootstrap`
6. Create another empty GameObject, attach `NetworkManager` and `PacketHandler`
7. Hit Play — it will attempt to connect to `127.0.0.1:7777`

## Project Structure

```
Assets/
  Scripts/
    GameBootstrap.cs          — Entry point, initiates connection
    Network/
      NetworkManager.cs       — TCP client, length-prefixed framing
      PacketHandler.cs        — Packet routing by type
    Player/
      PlayerController.cs     — Third-person movement + jump
    World/
      TerrainManager.cs       — Procedural terrain chunk streaming
      EntityManager.cs        — Networked entity lifecycle
    Proto/                    — Generated protobuf C# (from orlo-proto)
```

## Required Packages (via Package Manager)

- **Google.Protobuf** (NuGet or via Package Manager)
- **Unity.Mathematics** (for DOTS math types)
- **Addressables** (for asset streaming, Phase 2)

## Connecting to Dev Server

Run `orlo-server` locally:
```bash
cd ../orlo-server && mkdir build && cd build
cmake .. && make -j$(nproc)
./orlo-server 7777
```

Then hit Play in Unity — the client connects automatically.
