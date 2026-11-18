# Perfis por aplicativo e monitor

Em **Configurações → Perfis**, salve um perfil com a aparência e a disposição de widgets desejadas. Em **Perfis por monitor e aplicativo**, escolha o perfil salvo, selecione um monitor e/ou indique o executável local. Clique em **Salvar regra**. Uma nova regra com a mesma combinação substitui a anterior.

A troca de foco altera a aparência da barra do monitor em que o aplicativo está ativo. Uma regra que combina monitor e aplicativo tem prioridade sobre a regra do aplicativo. A regra do monitor é usada quando a janela ativa não corresponde a uma regra mais específica. Quando nenhuma regra corresponde, a barra volta às configurações globais.

Cada regra aplica tema, destaque, opacidade, tamanho da barra, seleção, ordem, grupos e escala de widgets. A topologia de monitores, os aplicativos fixados, os atalhos, o histórico, as credenciais e a inicialização com o Windows continuam sob controle das preferências globais. Os perfis e as regras ficam no banco SQLite do usuário. Exportar as configurações globais gera somente as preferências ativas.

A seleção do perfil usa o caminho do executável e o monitor informados pelo Windows para a janela em primeiro plano. Aplicativos com executável não identificável usam a regra do monitor ou a configuração global. A resolução ocorre em memória. O aplicativo só grava os dados quando a pessoa altera a configuração.
