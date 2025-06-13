# 🚀 GITorrent - BitTorrent-Inspired P2P Protocol

GITorrent is an **experimental peer-to-peer (P2P) protocol** for file sharing, inspired by the BitTorrent protocol.  
The system is composed of two main modules: **Tracker** and **PeerClient**.

---

## 🧩 Components

### 🛰️ Tracker (`GITorrent/Program.cs`)
- 📒 Maintains a registry of connected peers and the pieces (chunks) each peer owns.
- 📡 Receives requests via UDP on port `5000`.
- 🔄 Responds to new peers with the list of peers and their pieces.
- 📝 Updates the state of each peer's pieces as it receives notifications.

### 💻 PeerClient (`PeerClient/Program.cs`)
- 🧑‍💻 Each peer can be a **seeder** (has all pieces) or a **leecher** (downloads pieces from other peers).
- 🔗 Connects to the tracker to obtain the list of peers and available pieces.
- 📥 Requests pieces from other peers via TCP (`6000`).
- 📢 Notifies other peers about its presence in the network via TCP (`6001`).
- ⏰ Periodically updates the tracker with the pieces it owns.
- 💾 Stores downloaded pieces in a local folder (`C:\TorrentPieces` by default).

---

## ⚙️ How it works

1. **Tracker Initialization**  
   🛰️ The tracker is started and waits for peer connections on UDP port `5000`.

2. **Peer Initialization**  
   💻 The peer connects to the tracker, announces its presence, and receives the list of peers and available pieces.

3. **Piece Exchange**  
   - 📥 The peer requests missing pieces from other peers via TCP.
   - 💾 Upon receiving a piece, it saves it locally and notifies the tracker.
   - 🔔 Peers notify each other about new connections to facilitate dynamic discovery.

4. **State Update**  
   - ⏰ The peer periodically sends the tracker a list of pieces it owns.
   - 🌐 The tracker maintains the global state of the network.

---

## 🛠️ Requirements

- [.NET 9](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- C# 13.0

---

## ▶️ How to Run

1. **Build and run the _Tracker_ first.**
2. **Build and run one or more _PeerClient_ instances** on different machines or terminals.
3. Peers will automatically start exchanging pieces according to the implemented logic.

---

## 📌 Notes

- 📁 The pieces folder path is fixed at `C:\TorrentPieces` (can be changed in the code).
- 🔒 The ports used are:
  - Tracker: UDP `5000`
  - Peer TCP Server: `6000`
  - Peer Notification Server: `6001`

---

## 🌱 Next Steps

- 🤖 Implement automatic piece download when new peers are detected.
- 🛡️ Improve fault tolerance and dynamic peer list updates.
- 🖥️ Add CLI commands for easier usage.

---

> ⚠️ **Attention:**  
> This project is experimental, for educational purposes, and inspired by the BitTorrent protocol.

---

### Developed by Guilherme Siedschlag and Isabela Pietschmann
