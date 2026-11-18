# Roadmap do SoBar Advanced

A versão inicial concentra a barra, os widgets locais e os materiais visuais. O backlog tem **94 itens em 12 categorias**, dos quais 17 já têm um componente registrado em `WidgetService.Catalog`. A presença de um componente indica implementação no código. A aceitação em hardware específico e com contas de terceiros exige a verificação indicada em cada linha. Os outros 77 são candidatos e ficam fora do catálogo visível até terem uma fonte funcional. [Pesquisa de referência](LUMABAR_FEATURE_ANALYSIS.md) · [Resultados de validação](VALIDATION.md)

## Marcos e responsáveis

| Marco | Responsabilidade | Entrega e critério para avançar |
|---|---|---|
| Fundação | Arquitetura + testes | Solução modular, injeção das dependências, SQLite, logs, relógio e recuperação. Abrir com diretório novo, reiniciar e conferir preferências. |
| Integração de janelas | Windows + desempenho | Enumeração, ícones, alternância, DWM, AppBar e saída de emergência. Testes nativos exercitam processo controlado, restituição da área útil, dois monitores ativos (1920 × 1080 e 1360 × 768) e transferência de atalhos entre barras. Próxima bancada inclui Windows 10 e escalas físicas distintas. |
| Aparência e layout | Interface + Windows | Cinco estilos, três tamanhos, ordem e escala por widget. Conferir persistência, foco por teclado e recorte a 100%, 150% e 200%. Perfis por monitor e aplicativo com retorno à configuração global já implementados. Smoke executado em cinco temas, três tamanhos, quatro bordas e configurações a 390 DIP. O editor já cria, renomeia e remove grupos de widgets. Validar DPI misto físico. |
| Widgets e custo | Widgets + desempenho | Priorizar as 17 entradas iniciais. Medir inicialização fria, CPU, memória e handles com barra estável, vídeo e 50 janelas. Comparar amostras com os mesmos widgets e a mesma frequência. Rastrear painéis após 100 aberturas. |
| Central e conectores | IA + segurança | Busca, ações confirmadas, histórico optativo e conectores com autorização individual. Validar recusa, cancelamento e revogação. Liberar novos serviços em lotes pequenos após teste em conta autorizada. |
| Distribuição | Build + segurança | MSI e ZIP disponíveis, com verificação destacada RSA-PSS. Reproduzir instalação, atualização, rejeição de pacote adulterado e remoção a cada versão. Para lançamento público, testar assinatura, reputação e políticas de Windows em máquina limpa. |

**Ordem de expansão.** Primeiro estabilizar barra, editor, vidro e amostras locais. Em seguida entregar produtividade e mídia por APIs do Windows. Clima, e-mail e serviços externos entram por conector, conforme houver escopo de leitura, licença e conta para homologação. O progresso é registrado por widget e fonte.

**Critério comum de aceite.** Cada entrada precisa de ação ou métrica distinta, unidade e período quando cabíveis, dado observável no provedor e indicação inequívoca de erro ou indisponibilidade. O teste deve cobrir a primeira leitura, recuperação e desativação. Para rede, exigir consentimento, destino explícito, timeout, cancelamento, limitação de chamadas e teste de revogação. O tempo de atualização respeita as cotas e os direitos de uso do serviço.

Nas tabelas, **●** identifica os 17 componentes com código. As demais linhas são propostas. A última coluna contém o teste adicional de cada item.

### Relógios · 7

Fontes locais de data, fuso e calendário da plataforma. [TimeZoneInfo](https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo)

| Nº | Widget | Fonte | Aceite |
|---:|---|---|---|
| 1 | ● Relógio digital | Relógio local | Acompanhar a virada de minuto. |
| 2 | Relógio analógico | Hora local | Conferir ponteiros às 03h15 e 12h00. |
| 3 | Relógios mundiais | TimeZoneInfo | Comparar duas cidades com mudança de horário sazonal. |
| 4 | Janela de reunião | Horários úteis e fusos escolhidos | Calcular interseção que atravessa meia-noite. |
| 5 | Semana ISO | ISOWeek | Testar a semana que pertence ao ano ISO anterior. |
| 6 | Progresso do ano | Data e duração do ano civil | Validar 29 de fevereiro e último dia do ano. |
| 7 | Conversor de horário | TimeZoneInfo | Explicar horário local ambíguo ou inexistente. |

### Produtividade · 10

Fontes locais de notas, agenda e tempo. A importação de calendários depende de um arquivo escolhido pelo usuário. [Formato iCalendar](https://www.rfc-editor.org/rfc/rfc5545)

| Nº | Widget | Fonte | Aceite |
|---:|---|---|---|
| 8 | ● Notas | Arquivo de notas local | Editar, fechar e reabrir com texto preservado. |
| 9 | ● Calendário | Calendário do sistema | Navegar entre meses incluindo fevereiro bissexto. |
| 10 | ● Cronômetro | Contador monotônico | Iniciar, pausar, retomar e zerar. |
| 11 | ● Pomodoro | Máquina de estados e tempo local | Completar a transição foco–pausa. |
| 12 | Temporizador regressivo | Prazo monotônico | Emitir um aviso por término e permitir cancelar. |
| 13 | Lista de tarefas | Itens e estados locais | Completar, desfazer e recuperar ao reiniciar. |
| 14 | Próximo compromisso | Arquivo ICS selecionado | Resolver fuso, recorrência e exceções suportadas. |
| 15 | Prazo de projeto | Data-alvo fornecida | Distinguir prazo de hoje e prazo vencido. |
| 16 | Hábitos | Registros locais por data | Corrigir marcação e recalcular sequência. |
| 17 | Foco semanal | Ciclos de foco registrados com consentimento | Excluir ciclo cancelado do total. |

### Sistema · 14

Fontes do Windows e sensores acessíveis à conta do usuário. Informar o sensor e a unidade. O funcionamento varia por equipamento. [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) · [Processos .NET](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process) · [Native Wi-Fi](https://learn.microsoft.com/en-us/windows/win32/nativewifi/portal)

| Nº | Widget | Fonte | Aceite |
|---:|---|---|---|
| 18 | ● CPU | GetSystemTimes | Validar diferença entre amostras e primeira leitura. |
| 19 | ● GPU | Sensor de carga LibreHardwareMonitor | Identificar adaptador e mostrar indisponível sem sensor. |
| 20 | ● RAM | GlobalMemoryStatusEx | Comparar memória física com o Windows no mesmo intervalo. |
| 21 | ● Disco | DriveInfo da unidade do sistema | Conferir capacidade e espaço livre em bytes. |
| 22 | ● Rede | Contadores das interfaces ativas | Trocar de adaptador sem produzir taxa negativa. |
| 23 | ● Temperatura | Sensor térmico LibreHardwareMonitor | Identificar sensor e unidade, tratar leitura ausente. |
| 24 | ● Bateria | GetSystemPowerStatus | Distinguir máquina sem bateria de bateria descarregada. |
| 25 | Processos por CPU | Process.TotalProcessorTime | Normalizar por intervalo e processadores lógicos. |
| 26 | Processos por RAM | Process.WorkingSet64 | Tratar encerramento durante coleta. |
| 27 | Brilho integrado | WMI de monitor compatível | Ler nível e validar alteração solicitada em notebook. |
| 28 | Taxa de I/O do disco | Contadores de desempenho de disco | Conferir bytes por segundo numa cópia controlada. |
| 29 | Pressão de processos | Tabelas de processos e threads | Conferir contagem e tolerar acesso negado. |
| 30 | Tempo desde a inicialização | Contador de uptime Win32 | Manter progressão após suspensão. |
| 31 | Sinal Wi-Fi | Native Wi-Fi com permissão aplicável | Distinguir cabo, desconexão e falta de autorização. |

### Aplicativos e arquivos · 7

O serviço de desktop e a pesquisa já atendem funções da barra. As linhas desta categoria propõem cartões configuráveis com essas fontes. [Shell do Windows](https://learn.microsoft.com/en-us/windows/win32/shell/shell-entry)

| Nº | Widget | Fonte | Aceite |
|---:|---|---|---|
| 32 | Lançador de favoritos | Executáveis fixados pelo usuário | Detectar caminho removido antes de abrir. |
| 33 | Lista de janelas por monitor | DesktopWindowService | Mover janela entre telas e atualizar associação. |
| 34 | Arquivos recentes | Pasta Recent do usuário | Permitir excluir pastas e ignorar destino ausente. |
| 35 | Pastas favoritas | Diretórios selecionados | Abrir pasta com espaços e acentos. |
| 36 | Lixeira | SHQueryRecycleBin | Mostrar totais coerentes em duas unidades. |
| 37 | Tamanho de uma pasta | Enumeração local limitada | Cancelar varredura, evitar ciclos e sinalizar itens inacessíveis. |
| 38 | Pesquisa em pasta | Índice ou enumeração da raiz autorizada | Retornar resultado, respeitar limite e cancelar consulta antiga. |

### Mídia e áudio · 7

Controles básicos usam o dispositivo do Windows. Metadados dependem da adesão do reprodutor à sessão de mídia. [Core Audio](https://learn.microsoft.com/en-us/windows/win32/coreaudio/core-audio-apis-in-windows-vista) · [Sessões de transporte](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager)

| Nº | Widget | Fonte | Aceite |
|---:|---|---|---|
| 39 | ● Música e controles | Sessões Core Audio e comandos de mídia | Identificar aplicativo ativo e verificar ação com player compatível. |
| 40 | ● Volume | Endpoint de reprodução padrão | Ler, alterar e refletir mudança externa. |
| 41 | ● Microfone | Endpoint de captura padrão | Alternar mudo e tratar dispositivo removido. |
| 42 | Faixa atual | Metadados GSMTC | Refletir troca de faixa e reprodutor sem sessão. |
| 43 | Mixer por aplicativo | Sessões de volume Core Audio | Alterar a sessão escolhida mantendo as demais. |
| 44 | Dispositivos de saída | Enumeração de endpoints de áudio | Indicar padrão e identificar conexão/desconexão. |
| 45 | Posição da faixa | Timeline de sessão GSMTC | Mostrar duração e respeitar suporte do player a busca. |

### Inteligência artificial · 7

Uso de uma API de organização e franquia de aplicativo de consumo são métricas com escopos diferentes. Cada cartão deve indicar conta, unidade e janela temporal. A implantação de limites pessoais de ChatGPT, Claude, Gemini, Grok, DeepSeek e GLM aguarda mecanismo público autorizado e teste com conta habilitada. Nunca inferir franquia a partir de um gráfico de tokens. [OpenAI Usage](https://developers.openai.com/api/reference/resources/admin/subresources/organization/subresources/usage) · [Anthropic Usage and Cost](https://platform.claude.com/docs/en/manage-claude/usage-cost-api)

| Nº | Widget | Fonte | Aceite |
|---:|---|---|---|
| 46 | ● Uso de completions OpenAI | Usage API da organização, chave administrativa | Conciliar tokens e intervalo UTC com relatório autorizado. |
| 47 | Volume de embeddings | Usage API de embeddings | Comparar tokens de entrada e requisições no mesmo dia. |
| 48 | Custo da organização | API de custos OpenAI | Exibir moeda e conciliar período com faturamento. |
| 49 | Uso por modelo | Usage API com agrupamento por modelo | Reconciliar soma de grupos, incluindo registros sem modelo. |
| 50 | Consumo da API Claude | API administrativa Anthropic | Identificar organização e validar cache de tokens. |
| 51 | Custos da API Claude | Cost Report Anthropic | Conciliar itens monetários no período UTC. |
| 52 | Orçamento de IA | Limite informado e despesas autorizadas | Avisar uma vez por limiar e marcar atraso nos dados. |

### Análises · 8

Coleta local de atividade é optativa e precisa oferecer pausa, retenção e exclusão. Análises web exigem autorização no serviço. [Search Console](https://developers.google.com/webmaster-tools/v1/api_reference_index) · [Plausible Stats API](https://plausible.io/docs/stats-api)

| Nº | Widget | Fonte | Aceite |
|---:|---|---|---|
| 53 | Tempo por aplicativo | Mudanças de primeiro plano e inatividade local | Excluir bloqueio de sessão e inatividade. |
| 54 | Uso por hora | Intervalos de atividade local | Dividir corretamente um intervalo que cruza hora e meia-noite. |
| 55 | Categorias de uso | Classificação local definida pelo usuário | Reatribuir aplicativo e recalcular total. |
| 56 | Interrupções de foco | Trocas de aplicativo durante sessão optativa | Respeitar lista de apps do projeto. |
| 57 | Uso por domínio | Extensão de navegador opcional e consentida | Reter só o domínio e ignorar janelas privadas. |
| 58 | Cliques de busca | Google Search Console | Restringir à propriedade autorizada e exibir atraso. |
| 59 | Visitantes do site | Plausible Stats | Confirmar período e fuso com o painel da conta. |
| 60 | Origens de tráfego | Plausible com agrupamento por origem | Reconciliar tráfego direto e categorias pequenas. |

### Redes sociais · 6

Somente fontes públicas ou OAuth com escopo mínimo. Informar quando a plataforma arredonda ou oculta números. [YouTube Data API](https://developers.google.com/youtube/data/reference/rest) · [Contas Mastodon](https://docs.joinmastodon.org/methods/accounts/) · [Notificações Mastodon](https://docs.joinmastodon.org/methods/notifications/)

| Nº | Widget | Fonte | Aceite |
|---:|---|---|---|
| 61 | Inscritos no canal | YouTube channels.statistics | Tratar contagem oculta ou arredondada. |
| 62 | Visualizações do canal | YouTube channels.statistics | Conferir valor e data da amostra em canal autorizado. |
| 63 | Último vídeo | Playlist de uploads YouTube | Ignorar vídeo privado ou removido. |
| 64 | Seguidores Mastodon | Conta na instância selecionada | Resolver identificador federado e indicar instância. |
| 65 | Publicações recentes | Statuses públicos da conta Mastodon | Respeitar visibilidade e paginação. |
| 66 | Menções Mastodon | Notifications com autorização da conta | Mostrar apenas menções e remover credencial ao desconectar. |

### Ações e mercados · 7

A disponibilidade, a latência e os direitos de exibição dependem do plano do fornecedor. Indicar bolsa, moeda, horário e eventual atraso. [Documentação Alpha Vantage](https://www.alphavantage.co/documentation/)

| Nº | Widget | Fonte | Aceite |
|---:|---|---|---|
| 67 | Cotação | API de cotação com licença compatível | Distinguir fechamento anterior e negociação recente. |
| 68 | Lista de acompanhamento | Cotações para símbolos escolhidos | Tratar símbolo inválido e orçamento de chamadas. |
| 69 | Histórico de preço | Série diária do provedor | Preservar calendário de pregões e indicar ajustes. |
| 70 | Alerta de preço | Cotação e limiar definido localmente | Avisar só ao cruzar o limite. |
| 71 | Carteira informada | Quantidades locais e cotações | Separar moedas e informar posição sem cotação. |
| 72 | Eventos de dividendos | Endpoint de dividendos | Distinguir data ex, declaração e pagamento. |
| 73 | Câmbio | Taxa entre moedas do provedor | Indicar moeda-base e hora da taxa. |

### Finanças · 7

Dados de comércio exigem credenciais específicas, somente leitura, moeda e período. Testar estorno, paginação e divergência de fuso em conta de homologação. [Stripe API](https://docs.stripe.com/api) · [Polar](https://polar.sh/docs/api-reference/introduction) · [Dodo Payments](https://docs.dodopayments.com/api-reference/introduction)

| Nº | Widget | Fonte | Aceite |
|---:|---|---|---|
| 74 | Saldo disponível | Stripe Balance | Separar disponível e pendente por moeda. |
| 75 | Repasses | Stripe Payouts | Distinguir pago, em trânsito e falho. |
| 76 | Receita líquida | Stripe Balance Transactions | Conciliar taxas e estornos com extrato de teste. |
| 77 | Reembolsos | Stripe Refunds | Reconciliar reembolso parcial e total. |
| 78 | Pedidos Polar | Pedidos da organização Polar | Contar pedido uma única vez por ID. |
| 79 | Pagamentos Dodo | Lista de pagamentos Dodo | Incluir somente estados definidos pela métrica. |
| 80 | Receita mensal recorrente | Assinaturas e preços Stripe | Declarar regra de normalização para anual, desconto e cancelamento. |

### Clima · 7

Todas as propostas abaixo podem compartilhar coordenadas explicitamente configuradas e o cache de uma consulta ao provedor. [Open-Meteo Forecast API](https://open-meteo.com/en/docs)

| Nº | Widget | Fonte | Aceite |
|---:|---|---|---|
| 81 | ● Condição atual | Temperatura e código meteorológico | Conferir local, unidade, hora e atribuição. |
| 82 | Temperatura por hora | Série horária | Respeitar fuso do local e mudança sazonal. |
| 83 | Probabilidade de chuva | Precipitation probability | Distinguir probabilidade, volume e dado ausente. |
| 84 | Máxima e mínima | Séries diárias do provedor | Exibir dia local ao cruzar meia-noite UTC. |
| 85 | Vento | Velocidade, direção e rajadas | Indicar unidade e direção da origem. |
| 86 | Nascer e pôr do sol | Horários diários | Tratar ausência de evento em latitude extrema. |
| 87 | Índice UV | Série diária ou horária | Indicar horário de referência e máximo previsto. |

### Online e e-mail · 7

Exibir apenas metadados necessários, respeitar escopo concedido e permitir revogação. O leitor de feed exige HTTPS, isolamento da interpretação de conteúdo e limite de tamanho. [Gmail API](https://developers.google.com/workspace/gmail/api/reference/rest) · [Google Calendar](https://developers.google.com/workspace/calendar/api/v3/reference) · [GitHub REST API](https://docs.github.com/en/rest) · [Atom](https://www.rfc-editor.org/rfc/rfc4287)

| Nº | Widget | Fonte | Aceite |
|---:|---|---|---|
| 88 | Caixa de entrada não lida | Gmail labels INBOX | Conferir contagem de mensagens e expiração de token. |
| 89 | Marcador do Gmail | Contadores do marcador escolhido | Tratar marcador renomeado ou excluído. |
| 90 | Agenda conectada | Google Calendar events | Respeitar conta, fuso e eventos de dia inteiro. |
| 91 | Issues atribuídas | GitHub Issues | Filtrar itens abertos e excluir PRs do total. |
| 92 | Notificações GitHub | Notifications API | Lidar com paginação e cabeçalhos de atualização. |
| 93 | Revisões solicitadas | Pull requests em repositórios autorizados | Excluir PR fechado e revisão já satisfeita. |
| 94 | Leitor de novidades | Feed Atom escolhido | Validar redirecionamento, deduplicar ID e abrir link só após clique. |

