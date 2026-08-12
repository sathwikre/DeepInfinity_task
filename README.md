# WPF + Local Azure Functions — Login Sample

A local-only desktop application built with WPF (`LoginClient`) and a .NET Isolated Azure Function backend (`LoginFunction`). No Azure account or deployment is needed — everything runs on your machine.

## Features

| Feature | Description |
|---|---|
| Login | Validates credentials against a hardcoded admin account |
| File Reader | Extracts text from PDF and image files (OCR via Tesseract) |
| Audio Transcription | Transcribes `.mp3` and `.wav` files using local OpenAI Whisper (no cloud API) |

---

## Project layout

```
LoginSample/
├── LoginSample.slnx
├── LoginClient/                        # WPF desktop application (net8.0-windows)
│   ├── Models/
│   │   ├── LoginRequest.cs
│   │   ├── LoginResponse.cs
│   │   ├── FileReadResponse.cs
│   │   └── TranscriptionResponse.cs
│   ├── Pages/
│   │   ├── DashboardPage.xaml/.cs
│   │   ├── FileReaderPage.xaml/.cs
│   │   └── AudioTranscriptionPage.xaml/.cs
│   ├── Services/ApiService.cs
│   ├── MainWindow.xaml/.cs
│   └── DashboardWindow.xaml/.cs
└── LoginFunction/                      # HTTP-triggered Azure Function (net8.0)
    ├── Functions/
    │   ├── LoginFunction.cs
    │   ├── ReadFileFunction.cs
    │   └── TranscribeAudioFunction.cs
    ├── Helpers/
    │   └── MultipartFormHelper.cs
    ├── Models/
    │   ├── LoginRequest.cs
    │   ├── LoginResponse.cs
    │   ├── FileReadResponse.cs
    │   └── AudioTranscriptionResponse.cs
    ├── whisper_transcribe.py           # Python script invoked by TranscribeAudioFunction
    ├── Program.cs
    ├── host.json
    └── local.settings.json
```

---

## Prerequisites

### .NET / Azure Functions
- **Windows** (required — WPF and Media Foundation are Windows-only)
- **.NET 8 SDK** — targets `net8.0` / `net8.0-windows`
- **Azure Functions Core Tools v4** — install via npm:
  ```powershell
  npm install -g azure-functions-core-tools@4 --unsafe-perm true
  ```

### Python (for audio transcription)
- **Python 3.11** — required by `openai-whisper` and its `numba` dependency
- Install packages:
  ```powershell
  py -3.11 -m pip install openai-whisper
  ```
- On first transcription, Whisper automatically downloads the model weights (~75 MB for `tiny`, ~140 MB for `base`) to `~/.cache/whisper`.

### FFmpeg (for audio transcription)
Whisper uses FFmpeg internally to decode MP3 and other audio formats. FFmpeg must be on your system `PATH`.

- Download from: https://www.gyan.dev/ffmpeg/builds/ (get the **essentials** build)
- Extract and add the `bin` folder to your `PATH` environment variable
- Verify: `ffmpeg -version`

---

## Run locally

### 1. Restore and build

```powershell
cd c:\deep\.net\LoginSample
dotnet restore
dotnet build
```

### 2. Start the Azure Function backend

```powershell
cd c:\deep\.net\LoginSample\LoginFunction
func start
```

Wait until you see all three endpoints listed:
```
Functions:
    Login:            [POST] http://localhost:7071/api/login
    ReadFile:         [POST] http://localhost:7071/api/read-file
    TranscribeAudio:  [POST] http://localhost:7071/api/transcribe-audio
```

### 3. Start the WPF client

In a second terminal:

```powershell
cd c:\deep\.net\LoginSample\LoginClient
dotnet run
```

### 4. Login credentials

| Username | Password |
|---|---|
| `admin` | `1234` |

Any other non-empty credentials return **Invalid Credentials**.

---

## API endpoints

### POST /api/login

```json
{ "username": "admin", "password": "1234" }
```

Success:
```json
{ "success": true, "message": "Login Successful" }
```

Failure:
```json
{ "success": false, "message": "Invalid Credentials" }
```

---

### POST /api/read-file

Accepts `multipart/form-data` with a single file field named `file`.  
Supported formats: `.pdf`, `.png`, `.jpg`, `.jpeg`, `.bmp`, `.tiff`

Success:
```json
{
  "success": true,
  "fileName": "document.pdf",
  "content": "Extracted text here...",
  "message": ""
}
```

---

### POST /api/transcribe-audio

Accepts `multipart/form-data` with a single file field named `file`.  
Supported formats: `.mp3`, `.wav`

Success:
```json
{
  "success": true,
  "fileName": "recording.mp3",
  "transcript": "Transcribed text here...",
  "message": ""
}
```

Error:
```json
{
  "success": false,
  "fileName": "recording.mp3",
  "transcript": "",
  "message": "Reason for failure"
}
```

---

## Audio transcription — how it works

```
WPF selects .mp3 or .wav
  ↓
POST multipart/form-data → http://localhost:7071/api/transcribe-audio
  ↓
Azure Function writes uploaded bytes to a temp file
  ↓
Subprocess: py -3.11 whisper_transcribe.py <temp_file> tiny
  ↓
Whisper decodes audio via FFmpeg, runs speech-to-text locally
  ↓
Transcript printed to stdout, captured by C#
  ↓
Temp file deleted, JSON response returned to WPF
  ↓
WPF displays transcript
```

The Python script (`whisper_transcribe.py`) redirects all Whisper progress output to stderr so that **only the clean transcript text** reaches stdout, which C# reads.

### Configuration

These settings live in `LoginFunction/local.settings.json`:

| Setting | Default | Description |
|---|---|---|
| `WHISPER_PYTHON_EXE` | `py` | Python launcher. Change to a full path if `py` is not on your PATH |
| `WHISPER_MODEL` | `tiny` | Whisper model size: `tiny` · `base` · `small` · `medium` · `large` |

Model size trade-offs:

| Model | Size | Speed (CPU) | Accuracy |
|---|---|---|---|
| `tiny` | ~75 MB | Fastest (~10 min/hr audio) | Good for clear speech |
| `base` | ~140 MB | Fast (~25 min/hr audio) | Better accuracy |
| `small` | ~460 MB | Moderate | High accuracy |
| `medium` | ~1.5 GB | Slow | Very high accuracy |

---

## NuGet packages

### LoginClient
No third-party NuGet packages — uses only the built-in WPF / `System.Net.Http.Json` APIs.

### LoginFunction

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.Azure.Functions.Worker` | 2.1.0 | Isolated worker host |
| `Microsoft.Azure.Functions.Worker.Extensions.Http` | 3.3.0 | HTTP trigger binding |
| `Microsoft.Azure.Functions.Worker.Sdk` | 2.0.5 | Build SDK / analyzer |
| `Microsoft.AspNetCore.WebUtilities` | 8.0.0 | Multipart form parsing |
| `Microsoft.Net.Http.Headers` | 8.0.0 | Content-Type / boundary parsing |
| `PdfPig` | 0.1.15 | PDF text extraction |
| `Tesseract` | 5.2.0 | OCR for image files |

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| `func: command not found` | Install Azure Functions Core Tools: `npm install -g azure-functions-core-tools@4` |
| `whisper_transcribe.py was not found` | Run `dotnet build` — the script must be copied to the output directory |
| `ModuleNotFoundError: No module named 'whisper'` | Run `py -3.11 -m pip install openai-whisper` |
| `ffmpeg is not recognized` | Add FFmpeg `bin` folder to your system PATH |
| Empty transcript on valid audio | The audio may contain no speech, or try a larger model (`WHISPER_MODEL=base`) |
| Transcription hangs for a long time | Normal for long audio files on CPU — a 4-minute song takes ~3–8 min with `tiny` |
| `AzureWebJobsStorage` unhealthy warnings in func log | Safe to ignore — these functions are HTTP-only and don't use storage triggers |
| Login or file reader stopped working | Audio transcription changes touched only `TranscribeAudioFunction.cs` and the Python script — all other functions are unchanged |

---

## Important files

| File | Purpose |
|---|---|
| `LoginClient/Services/ApiService.cs` | All HTTP calls from the WPF app to the Function backend |
| `LoginClient/Pages/AudioTranscriptionPage.xaml.cs` | Audio file selection, upload, and transcript display |
| `LoginFunction/Functions/TranscribeAudioFunction.cs` | Receives the upload, spawns the Python subprocess, returns JSON |
| `LoginFunction/whisper_transcribe.py` | Loads the Whisper model and transcribes the audio file |
| `LoginFunction/Helpers/MultipartFormHelper.cs` | Parses `multipart/form-data` for all file upload endpoints |
| `LoginFunction/local.settings.json` | Local configuration — do not commit real secrets |
| `LoginFunction/local.settings.sample.json` | Safe committed copy of the settings template |
