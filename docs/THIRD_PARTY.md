# Componentes distribuídos

O pacote x64 contém o runtime .NET e Windows Desktop. Os arquivos de avisos que acompanham a publicação ficam junto ao executável. As versões de bibliotecas usadas na compilação estão nos arquivos de projeto e as dependências resolvidas no arquivo SoBar.deps.json.

| Componente | Finalidade | Fontes e licença |
|---|---|---|
| .NET e WPF | Runtime, janelas e desenho | [dotnet/runtime](https://github.com/dotnet/runtime), [dotnet/wpf](https://github.com/dotnet/wpf) |
| Microsoft.Data.Sqlite.Core e SQLitePCLRaw | Interface com a biblioteca SQLite do Windows | [EF Core](https://github.com/dotnet/efcore), [SQLitePCL.raw](https://github.com/ericsink/SQLitePCL.raw) |
| Serilog | Logs locais rotativos | [Serilog](https://github.com/serilog/serilog), [File sink](https://github.com/serilog/serilog-sinks-file) |
| LibreHardwareMonitorLib | Sensores de temperatura e GPU | [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor), [código da versão 0.9.6](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.6) |
| NAudio | Endpoints de áudio do Windows | [NAudio](https://github.com/naudio/NAudio) |
| WiX Toolset | Geração do MSI durante o build | [WiX](https://github.com/wixtoolset/wix) |

Clima e dados de localização são fornecidos pelo [Open-Meteo](https://open-meteo.com/) quando o usuário configura o widget. Aplicam-se as condições de acesso do provedor. A central de IA usa a conta OpenAI configurada pelo usuário.
