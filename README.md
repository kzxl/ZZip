# 🌌 ZeroZip — Ultra Compression & Instant SFX Packaging Studio

<p align="center">
  <a href="https://github.com/kzxl/ZeroZip"><img src="https://img.shields.io/badge/Type-Desktop%20Application%20%26%20CLI-007ACC?style=flat-square&logo=windows" alt="Type: Desktop App & CLI" /></a>
  <a href="https://github.com/kzxl/ZeroZip"><img src="https://img.shields.io/badge/Ecosystem-Zero%20Universe-8A2BE2?style=flat-square" alt="Ecosystem: Zero Universe" /></a>
  <a href="https://github.com/kzxl/ZeroZip"><img src="https://img.shields.io/badge/Platform-Windows%20x64-brightgreen?style=flat-square" alt="Platform: Windows x64" /></a>
  <a href="https://github.com/kzxl/ZeroZip"><img src="https://img.shields.io/badge/Distribution-Standalone%20Single--File-2ea44f?style=flat-square" alt="Distribution: Standalone Single-File" /></a>
</p>

<p align="center">
  <strong>High-ratio compression studio and instant self-extracting archive (SFX) creator for Windows</strong><br/>
  TAR Core + Zstandard/LZMA/Brotli Codecs • Append-Mode SFX • AES-256-GCM • Multiplatform Stubs
</p>

---


## 📖 Overview

**ZeroZip** (powered by the sovereign **Ztar** engine) packages directories and files into standalone `.exe` **self-extracting archives (SFX)**. Recipients run the generated executable directly with zero setup, zero extraction dependencies, and no .NET runtime requirements.

Unlike standard zip utilities, ZeroZip couples native POSIX TAR stream packaging with state-of-the-art compression algorithms (**Zstandard**, **LZMA**, **Brotli**) and authenticated **AES-256-GCM** encryption.

Part of the sovereign **ZeroUniverse** application suite, ZeroZip is engineered for developer deployments, game repacking, firmware distribution, and resilient file archives.

---

## 🌟 Key Features

- ⚡ **Instant Append-Mode SFX**: Builds self-extracting executables via zero-recompile binary tail-append. Recipients require no .NET SDK or runtime.
- 🗜️ **Multi-Algorithm Engine**:
  - **Zstandard (zstd)**: Ultra-fast multi-threaded compression with long-distance matching (LDM).
  - **LZMA**: Maximum compression ratio for dense source trees and distribution packages.
  - **Brotli**: Optimized web and text compression.
  - **Store**: Uncompressed tar packaging for rapid containerization.
- 🌌 **Long-Range Compression (`--long`)**: Up to 2GB sliding dictionary window for massive files (disk images, ISOs, virtual machine snapshots).
- 🎮 **Precomp Deep Repack Mode (`--precomp`)**: Unpacks pre-compressed streams (zlib, zip, png) within payloads and recompresses them using high-ratio codecs (game repacking technique).
- 🔐 **Authenticated AES-256-GCM Encryption**: Provides cryptographic integrity verification against file truncation or tampering.
- ✂️ **Multi-Part Volume Splitting**: Splits output into `.001`, `.002` chunks (FAT32-safe, email-friendly).
- 🛡️ **ZIP Envelope Cloaking (`--zip`)**: Envelopes the executable inside a standard `.zip` store wrapper to bypass restrictive mail and network firewall executable filters.
- ⏱️ **Rapid Ratio Estimation**: Predicts compression ratios in ~1 second before running long batch jobs.
- 🌍 **Cross-Platform Extraction**: Console extraction stubs available for Windows, Linux, and macOS.

---

## 🏗 Project Layout

| Project | Description |
| :--- | :--- |
| **`Ztar.Core`** | Core library: SFX binary format, compression/decompression pipeline, codecs, AES-GCM, ratio estimator |
| **`Ztar.Main`** | Primary application: Modern WinForms desktop GUI + unified CLI runner |
| **`Ztar.Stub`** | Native WinForms GUI extraction stub embedded into SFX executables |
| **`Ztar.StubConsole`** | Lightweight cross-platform console extraction stub |
| **`Ztar.Tests`** | Automated regression test suite (33 xUnit unit & integration tests) |

---

## 💻 CLI Usage

When executed with arguments, ZeroZip runs in headless CLI mode:

```bash
# Syntax
zerozip c <source> [-o output.exe] [-m zstd|lzma|brotli|store]
                   [--ultra|--normal|--fast] [--level N] [--window N] [--long]
                   [--threads N] [--split SIZE] [-p password] [--zip]
                   [--precomp] [--precomp-path <path>]
zerozip x <sfx.exe> [-o out_dir] [-p password]
zerozip i <sfx.exe>
zerozip l <sfx.exe> [-p password]
zerozip t <sfx.exe> [-p password]
zerozip e <source>
zerozip h
```

### Command Reference

| Command | Action | Description |
| :--- | :--- | :--- |
| `c` / `compress` | Compress | Package source directory or file into standalone SFX `.exe` |
| `x` / `extract` | Extract | Decompress payload to target directory |
| `i` / `info` | Info | Inspect archive metadata (codec, mode, sizes, CRC checksum) |
| `l` / `list` | List | Inspect archive file manifest without extracting to disk |
| `t` / `test` | Test | Verify integrity (CRC validation and trial decompression) |
| `e` / `estimate` | Estimate | Rapid ratio prediction (~1 sec) |
| `h` / `help` | Help | Display command-line options and examples |

### Practical Examples

```bash
# Compress directory with ultra Zstandard preset
zerozip c "C:\Data" -o Data.exe --ultra

# LZMA maximum compression with 2GB volume splits
zerozip c game.iso -m lzma --level 22 --long --split 2GB

# Password-protected archive enveloped in ZIP wrapper
zerozip c secret\ -o vault.exe -p "SecurePass123" --zip

# Inspect archive contents without unpacking
zerozip l Data.exe
```

---

## 🔨 Build from Source

### Prerequisites
- [.NET SDK 10 / .NET 8 SDK](https://dotnet.microsoft.com/download)
- Windows 10/11 (64-bit)

### Build Commands

```bash
# Build complete solution
dotnet build Ztar.slnx -c Release

# Run automated test suite
dotnet test Ztar.Tests/Ztar.Tests.csproj

# Package standalone release executable
./publish.ps1
```

The `publish.ps1` script produces a self-contained, single-file executable in `dist/`.

---

## 📄 License

Licensed under the **MIT License**. Part of the sovereign **ZeroUniverse** industrial computing ecosystem.
