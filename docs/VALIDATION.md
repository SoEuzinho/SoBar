# Validação da prévia 0.1.0

Em 18 de novembro de 2026, a solução compilou em Release com **zero avisos e zero erros** no Windows x64 build 26300. O SDK usado foi o 10.0.404. O MSI e o ZIP incluem o Windows Desktop Runtime 10.0.15.

## Resultados

| Verificação | Resultado observado |
|---|---|
| Integração Windows | 15 testes aprovados. Enumeração, ícone de processo externo, miniatura DWM, tela cheia controlada, reserva e devolução de área pelo Explorer. Duas barras ocuparam os monitores solicitados de 1920 × 1080 e 1360 × 768. A segunda tela tinha origem negativa em X. Ambas operavam a 96 DPI. O teste confirmou a transferência do registro de atalhos para a janela restante. |
| Widgets | 13 testes aprovados. Construção WPF dos 17 componentes, compartilhamento do relógio, contadores do sistema, foco, clima, DPAPI e contrato HTTP do consumo OpenAI. A consulta pública ao Open-Meteo retornou HTTP 200. |
| Infraestrutura, configurações e segurança | 17 testes aprovados. Incluem recuperação de SQLite corrompido, histórico optativo, aprovação do conteúdo de um manifesto, grupos, regras contextuais, assinatura, rejeição de pacote adulterado e lista de ações permitidas da IA. |
| Interface real | O smoke abriu barra, central e configurações, leu o relógio e exercitou as cinco páginas numa janela de 390 DIP. Aplicou cinco temas, três tamanhos, quatro bordas, ordem, escala e grupos. Verificou aplicação e restauração de um perfil e o processo auxiliar de extensões. |
| MSI e ZIP | Instalação por usuário, atalho do menu Iniciar, abertura do executável instalado, remoção e abertura da cópia extraída do ZIP passaram. Os testes usaram preferências temporárias. |
| Atualização | Instalamos um MSI de teste 0.0.9 e atualizamos para o MSI 0.1.0. O Windows Installer manteve um produto registrado, recusou o downgrade e abriu o executável atualizado. A assinatura destacada do MSI final conferiu com a chave pública incorporada. |
| Dependências | A consulta NuGet por vulnerabilidades conhecidas, incluindo dependências transitivas, não apontou pacotes nos nove projetos na data da compilação. |

**Total: 45 testes xUnit aprovados, zero falhas e zero ignorados**, além dos testes de instalação e execução da distribuição.

## Amostra de desempenho

Executamos o binário autossuficiente com configurações novas, relógio, CPU e RAM. Após três segundos de aquecimento, medimos doze amostras em 10,963 segundos, numa máquina com 24 processadores lógicos.

| Medida | Valor |
|---|---:|
| Tempo até o processo aceitar eventos de interface (WaitForInputIdle) | 2,635 s |
| Tempo de CPU durante a amostra | 218,75 ms |
| CPU média sobre toda a capacidade lógica da máquina | 0,083% |
| Memória residente (working set) | 122,8–135,5 MiB |
| Memória privada | 63,1–80,4 MiB |
| Handles no início e no fim | 611 e 611 |

Essa amostra caracteriza o custo da configuração inicial nesta máquina. Ensaios de longa duração, carga com muitas janelas e medições da GPU durante vídeo continuam pendentes.

## Reproduzir

Execute em uma sessão interativa do Windows. A opção abaixo inclui os testes que reservam temporariamente uma faixa do desktop e abrem janelas auxiliares. O teste de dois monitores requer duas telas em área estendida.

```powershell
dotnet build SoBar.sln -c Release
$env:SOBAR_NATIVE_SMOKE = '1'
dotnet test SoBar.sln -c Release --no-build
dotnet run --project src/SoBar.App -c Release -- --smoke-test
./scripts/Publish.ps1
./scripts/Test-Install.ps1 -Package ./artifacts/release/SoBar-0.1.0-win-x64.msi
dotnet package list --project SoBar.sln --vulnerable --include-transitive
```

Os scripts Test-Upgrade.ps1 e Measure-Idle.ps1 exercitam dois MSIs de versões diferentes e medem o executável autossuficiente, respectivamente. Use um perfil sem instalação anterior do SoBar para os testes de empacotamento.

## Cobertura pendente

Windows 10 físico, escalas físicas diferentes entre monitores, jogos em modo exclusivo, Smart App Control, dispositivos e sensores de outros fabricantes e uso prolongado ainda precisam de homologação. O efeito de vidro usa o compositor do Windows. A refração óptica por pixel vista no LumaBar é uma evolução prevista. As integrações OpenAI foram exercitadas com transporte HTTP controlado e proteção local de credenciais. Uma consulta faturável com chave real e a leitura de consumo de uma organização ainda dependem de credenciais autorizadas.
