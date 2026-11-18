# Widgets do SoBar

Em **Configurações → Widgets**, adicione um item à barra. Clique no item para abrir o painel. Cada tipo compartilha o estado entre monitores. O serviço conserva uma amostra por fonte e só agenda leituras para itens carregados ou com o painel aberto.

| Widget | Fonte e controles |
| --- | --- |
| Relógio e calendário | Hora local e calendário WPF navegável. |
| CPU e RAM | Variação dos contadores de tempo do Windows e ocupação da memória física. A CPU precisa de duas amostras. |
| Disco | Espaço disponível no volume que contém o Windows, em GiB. |
| Rede | Variação dos bytes enviados e recebidos pelas interfaces ativas, exceto loopback e túneis. |
| GPU e temperatura | O botão **Ativar sensores nesta sessão** abre o LibreHardwareMonitor. GPU mostra a maior carga de núcleo ou motor 3D disponível. Temperatura mostra o maior valor dos sensores de CPU e GPU acessíveis. O painel identifica a origem. |
| Música | Mostra um aplicativo com sessão de áudio de saída ativa. Os botões enviam os comandos de mídia do Windows ao reprodutor registrado. |
| Volume e microfone | Leitura, ajuste em passos de 5% e mudo do dispositivo multimídia padrão de saída ou entrada. |
| Bateria | Percentual de carga e fonte de alimentação informados pelo Windows. |
| Clima | Ao autorizar, envia latitude e longitude digitadas à previsão do Open-Meteo. Atualiza a temperatura e a condição a cada 15 minutos. As coordenadas valem para a sessão. [Documentação do Open-Meteo](https://open-meteo.com/en/docs) |
| Notas | Um bloco local, com gravação atômica em `notes.txt` no diretório de dados. Salva pelo botão e ao fechar o painel. |
| Cronômetro e Pomodoro | Medição monotônica, independente do relógio do sistema. O Pomodoro alterna 25 minutos de foco e 5 de pausa. O usuário inicia cada etapa. |
| Uso da API OpenAI | Consulta autorizada do consumo de Completions da organização, do início do dia UTC até a hora da coleta. O painel apresenta tokens de entrada, tokens de saída e número de solicitações. [API oficial de uso da OpenAI](https://platform.openai.com/docs/api-reference/usage/completions) |

## Conectar o widget de uso da API

1. Adicione **Uso da API OpenAI** nas configurações.
2. Abra o widget. Insira uma chave administrativa com permissão de leitura de uso da organização e selecione **Salvar chave e autorizar consultas**.
3. Consulte a soma de tokens na barra e o detalhamento no painel. A coleta periódica usa o intervalo de 15 minutos. Uma chave ausente, inválida ou sem permissão produz um estado explícito de indisponibilidade.
4. Para remover o acesso no aplicativo, use **Desconectar e apagar chave**. Para revogar a chave no provedor, use o painel administrativo da organização.

O widget envia um GET autenticado exclusivamente ao endpoint HTTPS `api.openai.com/v1/organization/usage/completions`. O SoBar guarda a chave em `usage-admin.dpapi`, cifrada pela DPAPI do usuário do Windows, separadamente da credencial do assistente. Remover o widget pausa a coleta quando o painel também estiver fechado. Esta integração mede tokens da API organizacional. Os limites pessoais de mensagens do ChatGPT não são expostos por esse endpoint.

## Verificação

A suíte `tests/SoBar.Widgets.Tests` exercita o cálculo da CPU, memória física e espaço em disco reais, coordenadas e interpretação de clima, estados do Pomodoro, construção WPF de todos os widgets, sincronização de dois relógios e descarte. Os testes de uso da API usam uma chave fictícia, DPAPI real e respostas HTTP controladas para validar o destino, o período UTC, os totais e a apresentação de falhas de autorização e de limite de consultas.

Respostas de hardware dependem do modelo, dos drivers, do dispositivo de áudio e das permissões da sessão. A interface informa quando a fonte está indisponível. A validação de consumo em uma organização real exige uma chave Admin fornecida por seu responsável.
