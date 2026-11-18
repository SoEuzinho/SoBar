# LumaBar e SoBar Advanced

Pesquisa de produto de 18 de novembro de 2026. O ponto de partida do SoBar é uma barra complementar com aplicativos, widgets e uma saída de emergência. O primeiro início usa sobreposição e mantém a barra do Windows disponível.

## Evidência pública

O README apresenta 85 widgets em 12 grupos. Na consulta ao HTML público do catálogo, contamos 109 cartões distribuídos em 13 categorias, excluídos os nove destaques repetidos. A categoria adicional é *Developer*. A contagem inclui variantes visuais e um item marcado como prévia. O planejamento do SoBar usa as 12 categorias solicitadas. [Site oficial](https://lumabar.xyz/) · [README oficial](https://github.com/Acadbek/lumabar#widgets) · [Catálogo oficial](https://lumabar.xyz/widgets)

| Grupo do catálogo | Cartões | Grupo do catálogo | Cartões |
|---|---:|---|---:|
| Relógios | 11 | Produtividade | 10 |
| Sistema | 23 | Aplicativos e arquivos | 7 |
| Mídia e áudio | 6 | IA | 11 |
| Análises | 4 | Redes sociais | 4 |
| Ações | 3 | Finanças | 15 |
| Clima | 6 | Online e e-mail | 6 |
| Developer | 3 | **Total** | **109** |

Na imagem pública do desktop, o dock fica na borda inferior. O controle de volume ocupa um cartão largo, ícones de aplicativos ficam no centro e os cartões de tempo de tela e uso de IA combinam números com pequenos gráficos. Data e hora ficam no extremo direito. Essa composição orienta o espaço que o SoBar deve reservar para controles de larguras diferentes. [Captura pública do desktop](https://github.com/Acadbek/lumabar/blob/main/assets/desktop-preview.webp)

Na captura do seletor, há busca no alto, categorias à esquerda e cartões de tamanhos diferentes. O cabeçalho informa 85 disponíveis e dez na barra. Sinais vermelhos de remoção aparecem sobre os itens enquanto o seletor está aberto. A imagem documenta o estado de edição. O gesto de arrastar e encaixar é descrito pelo desenvolvedor. [Imagem do seletor](https://github.com/Acadbek/lumabar/blob/main/assets/widgets-preview.webp) · [Fluxo de edição](https://github.com/Acadbek/lumabar#what-lumabar-is)

O autor descreve refração do conteúdo em movimento e suspensão desse trabalho quando a cena fica estável. A demonstração publicada inclui uma janela do OBS e a barra sobreposta. Tempo de GPU, latência, consumo ocioso e comportamento em máquinas diferentes exigem medição. A pesquisa utilizou material público, sem instalação autenticada, acesso a código privado ou engenharia reversa. [Demonstração do vidro](https://github.com/Acadbek/lumabar/blob/main/assets/glass-showcase.webp) · [Descrição do efeito](https://github.com/Acadbek/lumabar#see-it-in-action)

## Matriz comparativa

P0 corresponde ao primeiro uso seguro da barra, P1 ao uso diário e P2 à expansão. A complexidade considera dependências externas, estados da interface e diversidade de hardware. “No código” indica um caminho implementado. Quando há teste executado, a matriz indica o que foi verificado.

| Funcionalidade do LumaBar | Evidência ou fonte | Implementação proposta no SoBar | Prioridade | Complexidade | Dependências | Estado de desenvolvimento do SoBar |
|---|---|---|---|---|---|---|
| Aplicativos da barra e janelas abertas | [README](https://github.com/Acadbek/lumabar#what-lumabar-is) | Enumerar janelas, distinguir ativa, obter ícones, agrupar e manter fixações próprias | P0 | Alta | Win32, Shell, armazenamento | Enumeração, caminho e ícone de processo externo testados. Fixações e agrupamento no código. Importação das fixações do Explorer em estudo. |
| Dock no topo ou na base | [Descrição](https://github.com/Acadbek/lumabar#what-lumabar-is) | Posicionar por borda e monitor, reservar área via AppBar quando solicitado | P0 | Média | Shell AppBar, DPI | Quatro bordas testadas por geometria. Sobreposição, reserva superior e liberação testadas no Explorer real. |
| Recuo em jogo ou vídeo em tela cheia | [README](https://github.com/Acadbek/lumabar#what-lumabar-is) | Comparar a janela em primeiro plano com os limites físicos do monitor | P0 | Alta | Win32, DWM | Detecção com janela externa controlada testada. Jogos exclusivos e HDR requerem bancada. |
| Retorno da barra nativa ao sair | [Descrição](https://github.com/Acadbek/lumabar#what-lumabar-is) | Manter Explorer disponível, liberar registro e oferecer saída de emergência | P0 | Média | Ciclo de vida de HWND, atalhos | Restituição da área de trabalho verificada. Disponibilidade da combinação de teclas depende da sessão. |
| Pré-visualização | [Relato sobre prévias](https://github.com/Acadbek/lumabar/issues/8) | Usar miniatura ao vivo do compositor e liberá-la ao fechar | P1 | Média | DWM thumbnail | Miniatura de janela real apresentada em teste. Conteúdo protegido pode limitar a captura. |
| Monitor adicional solicitado | [Pedido #3](https://github.com/Acadbek/lumabar/issues/3) · [Pedido #9](https://github.com/Acadbek/lumabar/issues/9) | Uma barra por tela, filtragem por monitor e escolha da tela | P1 | Alta | Descoberta de telas, PerMonitorV2 | Duas barras verificadas simultaneamente em 1920 × 1080 e 1360 × 768, com área útil preservada e transferência de atalhos ao fechar a primeira. Perfis por monitor e aplicativo implementados. Escalas físicas distintas aguardam validação. |
| Cinco estilos e três tamanhos | [Estilos](https://github.com/Acadbek/lumabar#five-styles) | Escuro, Claro, Liquid Glass, iOS Glass e Midnight, três tamanhos, cor e opacidade | P1 | Média | WPF, composição do Windows | Smoke WPF executado com os cinco temas, os três tamanhos e as quatro bordas em configurações representativas; páginas de configurações verificadas a 390 DIP. Matriz completa de combinações e DPI em bancada pendente. |
| Vidro em movimento | [Guia](https://lumabar.xyz/guides/liquid-glass-windows) · [Relato #1](https://github.com/Acadbek/lumabar/issues/1) | Aplicar material do compositor e medir transições com vídeo reproduzível | P1 | Alta | DWM, composição por versão | Material e blur nativos no código. Refração geométrica e métricas de GPU são trabalho futuro. |
| Selecionar e arrastar widgets | [Seletor](https://github.com/Acadbek/lumabar/blob/main/assets/widgets-preview.webp) · [README](https://github.com/Acadbek/lumabar#what-lumabar-is) | Ativar, reordenar por arrastar ou botões e ajustar escala no editor | P1 | Média | WPF, perfis | Editor com ordem, escala e grupos nomeados. O smoke test verificou os blocos nas quatro bordas. Edição direta na barra em estudo. |
| Relógio e produtividade | [Catálogo](https://lumabar.xyz/widgets) | Hora local, calendário, notas, cronômetro e ciclos de foco | P0 | Média | Relógio do sistema, disco | Cinco módulos no catálogo inicial. Tarefas e compromissos importados planejados. |
| CPU, memória, rede, energia e sensores | [Catálogo](https://lumabar.xyz/widgets) | Amostrar sob demanda e indicar indisponibilidade por sensor | P1 | Alta | Win32, LibreHardwareMonitor, .NET | CPU, GPU, RAM, disco, rede, temperatura e bateria no catálogo. Disponibilidade depende da máquina. |
| Áudio e mídia | [Catálogo](https://lumabar.xyz/widgets) | Ler volume e mudo, comandar reprodução e identificar a sessão de áudio | P1 | Média | Core Audio, NAudio | Três módulos no código. Título, artista e posição da mídia aguardam integração. |
| Previsão do tempo | [Catálogo](https://lumabar.xyz/widgets) | Consultar condição atual para coordenadas fornecidas e identificar provedor | P1 | Média | HTTPS, Open-Meteo | Condição atual no código. Resumos horários, UV e precipitação planejados. |
| Uso e limites de IA | [Lista do autor](https://github.com/Acadbek/lumabar#widgets) | Consultar APIs autorizadas, identificando métrica, período e tipo de conta | P1 | Alta | Permissão do provedor, DPAPI | Consulta ao uso de completions da organização OpenAI implementada. Verificação ao vivo requer credencial. Limites de planos pessoais dependem de API autorizada. |
| Tempo de tela, apps e sites | [README](https://github.com/Acadbek/lumabar#widgets) | Medir duração em primeiro plano com consentimento, retenção e exclusão locais | P2 | Alta | Eventos de sessão, SQLite | Planejado. Medir domínios requer extensão de navegador opcional com consentimento próprio. |
| Serviços online, redes, cotações e pagamentos | [Catálogo](https://lumabar.xyz/widgets) | Conectores de leitura por provedor com cotas e desconexão | P2 | Alta | OAuth ou tokens, API e licença | Backlog por fonte no roadmap. Sessões e dados financeiros reais aguardam validação. |
| Layout e dados pessoais locais | [Privacidade](https://github.com/Acadbek/lumabar#privacy) | Preferências e perfis em SQLite, notas locais e segredos protegidos por usuário | P0 | Média | SQLite, DPAPI | Persistência no código. Histórico optativo. Consultas externas descritas abaixo. |
| MSI e atualização autenticada | [Distribuição](https://github.com/Acadbek/lumabar#this-repository) · [Issue #4](https://github.com/Acadbek/lumabar/issues/4) | Validar versão, hash e assinatura antes do Windows Installer | P0 | Alta | MSI, RSA-PSS, identidade do publicador | MSI e ZIP gerados. Assinatura destacada RSA-PSS implementada. Authenticode e validação de reputação com Smart App Control pendentes. |

## Relatos que orientam testes

As issues registram experiências individuais em versões e máquinas específicas. Elas dão origem aos cenários abaixo. A situação atual desses sintomas no LumaBar não foi reproduzida nesta pesquisa.

| Relato público | Cenário de aceitação para o SoBar |
|---|---|
| Fundo anterior na troca de janela e excesso de desfoque. [#1](https://github.com/Acadbek/lumabar/issues/1) · [#2](https://github.com/Acadbek/lumabar/issues/2) | Alternar janelas de alto contraste. Gravar transições nos temas de vidro, conferir legibilidade e medir GPU em repouso. |
| Solicitação de segunda barra e de conteúdo diferente no outro monitor. [#3](https://github.com/Acadbek/lumabar/issues/3) · [#9](https://github.com/Acadbek/lumabar/issues/9) | Usar monitores a 100% e 150%, mudar a tela principal, reconectar o secundário e conferir persistência e atalhos. |
| Ícones demoraram de 15 a 30 s para acompanhar abertura e fechamento. [#6](https://github.com/Acadbek/lumabar/issues/6) | Medir do evento externo à mudança visual com 5 e 50 janelas. Meta inicial de p95 de até 2,5 s. |
| Prévias vazias, atraso para fechar e conflito com Iniciar sobre jogo. [#7](https://github.com/Acadbek/lumabar/issues/7) · [#8](https://github.com/Acadbek/lumabar/issues/8) | Verificar clique, Escape, perda de foco, janela encerrada, conteúdo protegido e tecla Windows durante fullscreen. |
| MSI bloqueado por Smart App Control em uma máquina. [#4](https://github.com/Acadbek/lumabar/issues/4) | Exercitar instalação e atualização com Smart App Control ativo e registrar o identificador do signatário. |

## Diferenciais e privacidade

O SoBar acrescenta pesquisa local, central de comandos com confirmação, perfis manuais e extensões declarativas avaliadas por um processo auxiliar. Perfis contextuais aplicam aparência e layout conforme monitor e aplicativo. O sistema tenta a combinação específica, o aplicativo, o monitor e por último a configuração global. O editor reúne widgets vizinhos em grupos nomeados que acompanham o perfil. [Perfis contextuais](CONTEXT-PROFILES.md) · [Extensões](PLUGINS.md) · [Roadmap e backlog](ROADMAP.md) · [Resultados de validação](VALIDATION.md)

O clima envia coordenadas ao Open-Meteo após configuração. O widget de uso da API OpenAI envia um intervalo de tempo ao endpoint da organização quando há chave administrativa e o widget está ativo. A central de IA utiliza o texto enviado pelo usuário e sua credencial própria. Notas, configurações e histórico opcional ficam no computador. Os conectores futuros precisam declarar destino, escopo, retenção e processo de desconexão. [Open-Meteo](https://open-meteo.com/en/docs) · [API de uso da organização](https://developers.openai.com/api/reference/resources/admin/subresources/organization/subresources/usage)

