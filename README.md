# Ztar

Studio siêu nén và tạo tệp tự giải nén (SFX) gọn nhẹ cho Windows, kèm CLI và bộ giải nén đa nền tảng.

Ztar đóng gói thư mục/tệp thành một tệp `.exe` **tự giải nén** — người nhận chỉ cần chạy, không cần cài đặt gì. Lõi nén dùng TAR + zstd/LZMA/Brotli (không phải định dạng ZIP), nên tên là **Z**std + **tar** = Ztar.

## Tính năng

- **SFX tức thì**: dựng tệp tự giải nén bằng mô hình append (ghép đuôi), không cần .NET SDK trên máy người dùng, không biên dịch lại.
- **Nhiều thuật toán**: Zstandard (nhanh, đa luồng, LDM), LZMA (nén sâu nhất), Brotli, hoặc Store (chỉ đóng gói).
- **Nén tầm xa** (`--long`): cửa sổ tới 2GB cho tệp rất lớn (game, ISO, ảnh máy ảo).
- **Nén sâu kiểu repack** (`--precomp`): bung ngược các luồng đã nén sẵn (zlib/zip/png...) rồi nén lại — kỹ thuật của các bản repack game. Cần `precomp.exe` (tự dò; được nhúng vào SFX để máy nhận không cần cài).
- **Mã hóa AES-256-GCM** có xác thực: phát hiện sửa đổi/cắt cụt, không chỉ chống lỗi ngẫu nhiên.
- **Cắt mảnh**: chia thành nhiều phần `.001`, `.002`... (an toàn FAT32, dễ gửi).
- **Bọc ZIP để gửi** (`--zip`): gói SFX vào `.zip` (Store) để né bộ lọc chặn `.exe`.
- **Ước tính nhanh**: dự đoán tỉ lệ nén trong ~1 giây, cảnh báo dữ liệu khó nén.
- **Kiểm tra & liệt kê**: xem nội dung và kiểm tra toàn vẹn không cần giải nén.
- **Hủy giữa chừng**, tiến độ thật, kiểm tra CRC32.
- **Giải nén đa nền tảng**: stub console chạy Windows/Linux/macOS.

## Cấu trúc dự án

| Project | Mô tả |
|---|---|
| `Ztar.Core` | Thư viện lõi: định dạng SFX, engine nén/giải nén, codec, mã hóa, ước tính. |
| `Ztar.Main` | Ứng dụng chính: GUI WinForms + CLI. Build ra `Ztar.exe`. |
| `Ztar.Stub` | Bộ giải nén WinForms (Windows), nhúng vào `Ztar.exe`. |
| `Ztar.StubConsole` | Bộ giải nén console đa nền tảng. |
| `Ztar.Tests` | Bộ test xUnit (33 test). |

## Dùng bằng giao diện (GUI)

Chạy `Ztar.exe` không kèm tham số để mở giao diện:

1. Chọn nguồn (thư mục hoặc tệp).
2. Chọn nơi lưu `.exe` SFX.
3. Chọn thuật toán, mức nén, mật khẩu (tùy chọn), cắt mảnh, bọc ZIP, nén sâu.
4. Bấm **Kiểm tra nhanh** để ước tính tỉ lệ trước khi nén.
5. Bấm **Bắt đầu Siêu Nén**.

## Dùng bằng dòng lệnh (CLI)

`Ztar.exe` khi có tham số sẽ chạy ở chế độ CLI.

```
Ztar c <nguồn> [-o ra.exe] [-m zstd|lzma|brotli|store]
               [--ultra|--normal|--fast] [--level N] [--window N] [--long]
               [--threads N] [--split KÍCH_THƯỚC] [-p mật_khẩu] [--zip]
               [--precomp] [--precomp-path <đường>] [--precomp-args <args>]
Ztar x <sfx.exe> [-o thư_mục] [-p mật_khẩu]
Ztar i <sfx.exe>
Ztar l <sfx.exe> [-p mật_khẩu]
Ztar t <sfx.exe> [-p mật_khẩu]
Ztar e <nguồn>
Ztar h
```

| Lệnh | Chức năng |
|---|---|
| `c` / `compress` | Nén nguồn thành SFX `.exe` |
| `x` / `extract` | Giải nén một SFX |
| `i` / `info` | Xem thông tin (thuật toán, chế độ, kích thước, CRC) |
| `l` / `list` | Liệt kê nội dung, không giải nén |
| `t` / `test` | Kiểm tra toàn vẹn (CRC + giải nén thử), không ghi đĩa |
| `e` / `estimate` | Ước tính nhanh tỉ lệ nén |
| `h` / `help` | Trợ giúp |

### Ví dụ

```
Ztar c "C:\Data" -o Data.exe --ultra
Ztar c game.iso -m lzma --level 22 --long --split 2GB
Ztar c secret\ -o s.exe -p "MatKhau123" --zip
Ztar c game\ -o repack.exe --precomp -m lzma
Ztar e "C:\Data"
Ztar l Data.exe
Ztar t Data.exe
Ztar x Data.exe -o C:\Out
```

Nhấn `Ctrl-C` để hủy giữa chừng (lần nhấn đầu dừng êm, lần hai buộc thoát).

## Build và phát hành

Yêu cầu .NET SDK 10.

```powershell
# Build toàn bộ
dotnet build Ztar.slnx -c Release

# Chạy test
dotnet test Ztar.Tests/Ztar.Tests.csproj

# Phát hành bản gọn để gửi (1 tệp, không cần .NET runtime)
./publish.ps1
```

`publish.ps1` tạo `Ztar.exe` self-contained single-file trong thư mục `dist/`.

## Ghi chú về nén sâu (precomp)

Để đạt tỉ lệ kiểu repack game, `--precomp` cần `precomp.exe` (của Schnaader). Ztar **không kèm sẵn** công cụ này; đặt `precomp.exe` cạnh `Ztar.exe`, trong `tools/`, hoặc trong PATH. Khi tạo SFX có precomp, `precomp.exe` được nhúng vào payload để máy người nhận giải nén được mà không cần cài.

## Giấy phép

MIT — xem [LICENSE](LICENSE).
