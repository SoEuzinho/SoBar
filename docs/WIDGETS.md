# Widgets do SoBar

Em **Configurações → Widgets**, adicione um item à barra. Clique no item para abrir o painel. Cada tipo compartilha o estado entre monitores. O serviço conserva uma amostra por fonte e só agenda leituras para itens carregados ou com o painel aberto.

| Widget | Fonte e controles |
| --- | --- |
| CPU e memória | Cartão de duas linhas com percentuais e medidores verdes. Usa as mesmas amostras de CPU e RAM dos widgets individuais. |
| Agenda local | Próximo compromisso com mês, dia, título e hora local. Clique no cartão e em **Editar agenda** para adicionar um compromisso. Escolha um existente para editar ou remover. O arquivo local `agenda.json` usa gravação atômica. |
| Energia | Cartão azul com o percentual real da bateria ou a fonte de alimentação informada pelo Windows. |
| Data, hora e clima | Dia da semana, data e hora local. **Configurar clima** ativa a mesma autorização, localização e cache do widget de clima. |
| Tempo de tela | Coleta local opcional por processo, resumo diário ou dos últimos sete dias, distribuição por hora e lista por aplicativo. |
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

## Limites do Codex

Clique no cartão **Limites do Codex**, selecione **Conectar Codex instalado** e confira o caminho de codex.exe antes de autorizar. Use **Desconectar** para revogar a consulta automática. O SoBar guarda o caminho autorizado em codex-limits.opt-in e usa o login gerenciado pelo próprio Codex.

As duas barras mostram o percentual **já usado** em cada janela fornecida pela conta. O painel apresenta as datas de renovação em hora local. Quando a conta fornece janelas de 5 horas e de uma semana, o cartão as identifica como **5 h** e **Semana**. O widget consulta o processo local por entrada e saída padrão a cada 15 minutos enquanto estiver em uso. Uma conta que fornece só uma janela deixa a segunda leitura indisponível. [Protocolo oficial do Codex](https://developers.openai.com/codex/app-server/)

### Tempo de tela

Adicione **Tempo de tela**, abra o painel e escolha **Autorizar coleta local**. O SoBar amostra o processo em primeiro plano e soma intervalos observados de uso em baldes horários. A medição pausa após 60 segundos sem entrada, em bloqueio, suspensão, troca de sessão ou remoção do último widget de tempo de tela da barra. Intervalos longos entre amostras e trocas de aplicativo não recebem duração estimada.

O histórico contém somente nome do processo, hora e segundos de uso, em `screen-time.json`. A autorização fica em `screen-time.opt-in`. A exibição agrega horas no fuso local e considera até sete dias de calendário, contando hoje. **Pausar coleta** preserva o histórico e revoga a autorização. **Apagar histórico** remove os dados locais. A coleta só recomeça se estiver autorizada e o widget estiver na barra. O conteúdo das janelas, páginas visitadas e teclas digitadas não são lidos.

### Testes

A suíte `tests/SoBar.Widgets.Tests` exercita o cálculo da CPU, memória física e espaço em disco reais, coordenadas e interpretação de clima, estados do Pomodoro, construção WPF de todos os widgets, sincronização de dois relógios e descarte. Os testes de uso da API usam uma chave fictícia, DPAPI real e respostas HTTP controladas para validar o destino, o período UTC, os totais e a apresentação de falhas de autorização e de limite de consultas.

A suíte também verifica criação, alteração, ordenação, persistência e remoção de compromissos, energia em desktops e notebooks, consentimento de tempo de tela, ausência de registros antes da autorização, transição de horas, expiração de sete dias, lacunas de suspensão e apagamento do histórico.

Respostas de hardware dependem do modelo, dos drivers, do dispositivo de áudio e das permissões da sessão. A interface informa quando a fonte está indisponível. A validação de consumo em uma organização real exige uma chave Admin fornecida por seu responsável.
