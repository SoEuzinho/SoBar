# Desenvolvimento do SoBar

Use C# e WPF, com tipos anuláveis ativos. Mantenha a camada Core livre de WPF e de chamadas ao sistema operacional. App compõe as dependências por construtor e controla o ciclo de vida das janelas. Windows é dono dos handles, mensagens e registros AppBar. Widgets é dono das fontes de dados e dos temporizadores. Infrastructure concentra armazenamento, busca, IA e validação de atualizações. Plugins define a linguagem declarativa de extensões.

Distribua alterações por módulo. Coordene mudanças de contratos antes de modificar um consumidor. A equipe de integração revisa as referências entre projetos. A equipe de desempenho verifica inicialização, trabalho em segundo plano e liberação de recursos. A equipe de distribuição cuida do MSI e da identidade de assinatura.

Use dados observados. Mostre indisponibilidade quando faltar uma fonte. Descarte recursos nativos, inscrições em eventos, timers e streams. Trate cancelamento como parte do fluxo normal. Passe credenciais apenas ao destino autorizado e exclua segredos dos logs.

Para aceitar um marco, compile em Release, rode os testes disponíveis e exercite o caminho normal da interface afetada. Registre ambiente, limitações e resultados reproduzíveis em docs. Teste integração com dados temporários. A barra nativa e o Explorer devem permanecer disponíveis durante a execução e após falhas. Não acrescente execução arbitrária de comandos em plugins ou na central de IA.
