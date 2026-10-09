# Funções e limites — NEXUS 0.6

O pedido pretende cobertura das funções TL Optimizer com código/design próprios e gratuito. A 0.6 amplia a implementação, mas não anuncia paridade completa nem arranque validado neste PC. O inventário integral de 140 entradas e estados está em [TL-PARITY.md](TL-PARITY.md), com fonte JSON embebida e catálogo na interface. [README.md](README.md) descreve cada execução e [VALIDATION.md](VALIDATION.md) separa build, testes e efeitos no Windows.

| Área | Implementação atual | Limites materiais |
|---|---|---|
| Dashboard / PC Boost | CPU/RAM/hardware, gráficos, uptime, análise, diferenças de ajustes e reaplicação | Não é o motor Premium do TL; sem utilização GPU real ou ganho garantido |
| Energia e interface | Planos existentes, animações e 13 preferências, undo | Não cria plano agressivo, altera USB/PCIe/core parking ou paginação |
| Game Mode / App Tuner | Biblioteca escolhida, deteção de executáveis abertos, cinco perfis, sessões com undo | Não importa bibliotecas automaticamente nem configura cada jogo/programa/GPU; automático opt-in nesta abertura; sem gerir apps de fundo |
| Memória | RAM/processos; working set de app de fundo escolhida | Sem standby/cache global/páginas modificadas; pode aumentar acesso ao disco; sem undo |
| Rede | Informação, quatro DNS comparados, DNS IPv4 WMI reversível, automático, flush e renovação DHCP | Admin para alterações; sem VPN/DoH/NRPT; não é ping do jogo; flush/IP sem undo |
| Latência | ICMP ao IP escolhido, cinco amostras, perda/jitter | Não mede latência de entrada ou FPS |
| Benchmark | SHA-256/cópia, comparação por ambiente, histórico | Sem FPS/benchmark real de jogo ou causalidade atribuída a ajustes |
| Limpeza | TEMP/D3DSCache/Epic webcache/Recent presentes, revisão de ficheiros antigos e Lixeira | 7 dias; não todos os launchers; sem Prefetch/system Temp/esvaziar Lixeira; shaders podem causar pausas |
| Auto limpeza | Local Temp; 15/30/60 min; opt-in, 30 dias, limites, suspende por sessão/apps detetadas | Só enquanto aberta; não reconhece todos os jogos; não esvazia Lixeira nem limpa RAM |
| Windows / privacidade | 7 políticas de utilizador, edição/snapshot/undo; ferramentas oficiais | Eficácia depende de edição/conta; não inclui catálogo completo de políticas ou telemetria de terceiros |
| Apps nativas | 28 nomes opcionais, consulta, remoção individual e pesquisa Store | Sem remoção/reinstalação em massa; remoção perde dados, não tem undo; apps antigas podem não existir |
| Histórico / recuperação | SQLite, undo verificado, sessões/eventos/benchmark, backup e restauro global | Sem importação/restauro integral; eventos de ações irreversíveis não têm undo |
| Assinatura | Publicação x64 completa, tratamento do bloqueio | Continua sem assinatura confiável; não está pronta neste PC |

Não são implementadas ações para desativar segurança, atualizações ou recuperação. Serviços, drivers, modo de espera moderno, energia de dispositivos e paginação exigem avaliação individual; não são desativados como otimização genérica. O catálogo mantém esses limites e as pendências visíveis.

A 0.6 acrescenta duas ferramentas próprias de verificação: importação/análise de capturas PresentMon e medição passiva de CPU por aplicação. São implementações com limites, sem equivalência presumida ao motor TL. Ver FPS-VALIDATION.md e TIKTOK-REVIEW.md. Testes de lógica e round-trip real de animações em VALIDATION.md. A interface 0.6 ainda exige arranque/validação visual.

A 0.6 reorganiza a navegação e acrescenta 14 cartões de preferências, políticas e Teclas de Aderência diretas, PC Boost em conjunto com recuperação de falhas, Game Mode automático com autorização fixa, App Tuner e Auto Limpeza em página própria. Desligar um cartão repõe o valor anterior guardado. Fechar normalmente tenta repor a sessão ativa; alterações permanentes precisam de restauro explícito.
