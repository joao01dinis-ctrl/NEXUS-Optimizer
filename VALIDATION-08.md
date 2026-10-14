# Validação NEXUS 0.8

Estado em 14/10/2026: revisão em testes, não publicada na Microsoft Store.

- A solução compilou em Release com .NET 10.0.401: zero avisos e zero erros.
- O teste local de arranque recusou carregar Nexus.Optimization.dll devido ao Controlo de Aplicações do Windows, código 0x800711C7. Não foram alteradas proteções.
- A execução local dos testes foi abortada porque o testhost não ligou em noventa segundos. Não conta como testes aprovados.
- Validação GitHub Windows da revisão em preparação: suíte completa, preferência nativa e rato com undo num runner descartável, arranque/capturas das páginas novas, tema claro e análise sem escrita. Chamadas OpenAI apenas simuladas em memória nos testes.
- OpenAI real não validada: não há chave configurada. Nenhuma chamada real foi feita.
- Sem medições de melhorias no FiveM, sem equivalência integral demonstrada ao PrecisionFix ou TL.
- A submissão Microsoft Store 0.7 anterior é independente. Esta revisão não a cancela nem altera.

O pacote completo precisa de assinatura/distribuição aceite pelo PC e de arranque validado no destino antes de ser anunciado como utilizável.
