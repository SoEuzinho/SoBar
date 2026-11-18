# Compatibilidade do Windows

A primeira distribuição destina-se a PCs Windows x64. A publicação inclui o runtime Windows Desktop do .NET 10. A compilação dos fontes requer o SDK .NET 10. Use uma instalação do Windows com interface gráfica e uma sessão de usuário com Explorer.

| Ambiente | Situação |
|---|---|
| Windows 11 x64 | Alvo principal. A build e os resultados locais estão no [registro de validação](VALIDATION.md). |
| Windows 10 Enterprise/LTSC e IoT | A Microsoft lista versões específicas para .NET 10. As APIs e os sensores ainda exigem testes físicos por build e driver. |
| Windows 10 Home/Pro 22H2 | A matriz atual da Microsoft não relaciona essa edição como alvo de suporte do .NET 10. A compatibilidade em execução precisa de validação separada. |
| Windows x86 ou Arm64 nativo | Sem pacote nesta versão. |

Para uma frota Windows 10 comum que exija suporte formal, a alternativa preferível é adotar um Windows com ciclo de suporte compatível. Se a migração for inviável, uma variante para .NET Framework 4.8/4.8.1 requer portar dependências, persistência, HTTP e testes. O Framework acompanha o ciclo de suporte do próprio Windows. Um downgrade de runtime isolado não prolonga o ciclo da edição do sistema. Consulte a [matriz oficial de .NET 10](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md) e a [política do .NET Framework](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-framework).

WPF e SQLite usam os recursos locais do Windows. O provedor SQLite aponta para winsqlite3.dll. O áudio depende de endpoint WASAPI ativo. GPU e temperatura dependem dos sensores expostos ao LibreHardwareMonitor e dos privilégios já disponíveis. A composição de vidro depende do compositor, da versão do sistema, de transparência e da sessão gráfica. Uma sessão remota, uma VM ou um driver sem suporte pode produzir resultados diferentes. A execução cotidiana usa o token normal do usuário.
