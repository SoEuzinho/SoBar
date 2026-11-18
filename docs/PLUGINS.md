# Extensões declarativas v1

Em Configurações, abra Extensões, selecione um manifesto JSON e revise o nome, a fonte e as permissões. O SoBar instala o conteúdo revisado. Um arquivo substituído depois da prévia não muda a permissão aprovada.

| Campo | Contrato |
|---|---|
| apiVersion | Inteiro 1 |
| id | Até 40 caracteres, começa com letra minúscula, admite letras a–z, dígitos e hífen |
| name | Até 48 caracteres |
| source | text, utc ou uptime |
| permissions | Lista vazia para texto, uma entrada clock.read para UTC ou uptime.read para tempo ligado |
| text | Conteúdo literal de até 100 caracteres para a fonte text |

Um processo auxiliar inicia sob demanda para validar um único documento de até 8 KiB. A comunicação usa pipes e o processo tem prazo de oito segundos. A barra recebe um registro de dados e desenha texto WPF. O contrato só oferece as três operações da tabela. DLLs de terceiros, scripts, expressões, HTML/XAML, acesso a arquivos e requisições de rede estão fora da linguagem suportada. O processo separado é uma barreira de falhas; as restrições de capacidade vêm da linguagem declarativa.

O exemplo [relógio UTC](examples/utc.sobar.json) pode ser importado diretamente. A revisão da API deve preservar manifestos já aceitos e exigir novo consentimento para capacidades adicionais.
