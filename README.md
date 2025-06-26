# 🚀 GITorrent - BitTorrent-Inspired P2P Protocol (English Version) 🇺🇸

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

4. **State Update**  
   - ⏰ The peer periodically sends the tracker a list of pieces it owns.
   - 🌐 The tracker maintains the global state of the network.

---

## 🛠️ Requirements

- [.NET 9](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- C# 13.0

---

## ▶️ How to Run

1. Download and install SDK on your machine on [SDK Download](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-9.0.301-windows-x64-installer);
2. Inside the GITorrent project, enter the folder PeerClient or Tracker (it depends what role you want to execute);
3. Open the terminal in the folder you opened;
4. Execute the `dotnet build` command;
5. With the build done, execute the `dotnet run` command;
6. Then your PeerClient/Tracker will be running.

---

## 📌 Notes

- 📁 The pieces folder path is fixed at `C:\TorrentPieces` (can be changed in the code).
- 🔒 The ports used are:
  - Tracker: UDP `5000`
  - Peer TCP Server: `6000`
    
---

> ⚠️ **Attention:**  
> This project is experimental, for educational purposes, and inspired by the BitTorrent protocol.

---
# 🚀 GITorrent - Protocolo P2P Inspirado no BitTorrent (Versão em Português) 🇧🇷

GITorrent é um **protocolo experimental peer-to-peer (P2P)** para compartilhamento de arquivos, inspirado no protocolo BitTorrent.  
O sistema é composto por dois módulos principais: **Tracker** e **PeerClient**.

---

## 🧩 Componentes

### 🛰️ Tracker (`GITorrent/Program.cs`)
- 📒 Mantém um registro dos peers conectados e dos pedaços (chunks) que cada peer possui.
- 📡 Recebe requisições via UDP na porta `5000`.
- 🔄 Responde a novos peers com a lista de peers e os pedaços disponíveis.
- 📝 Atualiza o estado dos pedaços de cada peer conforme recebe notificações.

### 💻 PeerClient (`PeerClient/Program.cs`)
- 🧑‍💻 Cada peer pode ser um **seeder** (possui todos os pedaços) ou **leecher** (baixa pedaços de outros peers).
- 🔗 Conecta-se ao tracker para obter a lista de peers e pedaços disponíveis.
- 📥 Solicita pedaços de outros peers via TCP (`6000`).
- ⏰ Atualiza periodicamente o tracker com os pedaços que possui.
- 💾 Armazena os pedaços baixados em uma pasta local (`C:\TorrentPieces` por padrão).

---

## ⚙️ Como funciona

1. **Inicialização do Tracker**  
   🛰️ O tracker é iniciado e aguarda conexões de peers na porta UDP `5000`.

2. **Inicialização do Peer**  
   💻 O peer conecta-se ao tracker, anuncia sua presença e recebe a lista de peers e pedaços disponíveis.

3. **Troca de Pedaços**  
   - 📥 O peer solicita os pedaços que faltam de outros peers via TCP.
   - 💾 Ao receber um pedaço, salva-o localmente e notifica o tracker.
   - 🔔 Os peers notificam uns aos outros sobre novas conexões para facilitar a descoberta dinâmica.

4. **Atualização de Estado**  
   - ⏰ O peer envia periodicamente ao tracker a lista de pedaços que possui.
   - 🌐 O tracker mantém o estado global da rede.

---

## 🛠️ Requisitos

- [.NET 9](https://dotnet.microsoft.com/pt-br/download/dotnet/9.0)
- C# 13.0

---

## ▶️ Como executar

1. Baixe e instale o SDK em sua máquina pelo [SDK Download](https://dotnet.microsoft.com/pt-br/download/dotnet/thank-you/sdk-9.0.301-windows-x64-installer);
2. Dentro do projeto GITorrent, entre na pasta PeerClient ou Tracker (dependendo do papel que deseja executar);
3. Abra o terminal na pasta escolhida;
4. Execute o comando `dotnet build`;
5. Com o build concluído, execute o comando `dotnet run`;
6. Pronto, seu PeerClient/Tracker estará rodando.

---

## 📌 Observações

- 📁 O caminho da pasta de pedaços está fixado em `C:\TorrentPieces` (pode ser alterado no código).
- 🔒 As portas utilizadas são:
  - Tracker: UDP `5000`
  - Peer TCP Server: `6000`
    
---

> ⚠️ **Atenção:**  
> Este projeto é experimental, para fins educacionais, e foi inspirado no protocolo BitTorrent.

---

### Desenvolvido por Guilherme Siedschlag e Isabela Pietschmann

---

### Developed by Guilherme Siedschlag and Isabela Pietschmann
