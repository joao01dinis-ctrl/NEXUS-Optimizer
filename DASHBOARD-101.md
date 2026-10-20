# NEXOS 1.0.1 — painel e distribuição local

Pedido de 20/10/2026: painel inspirado na referência preta/verde, preservar dados e funcionalidades, entregar uma pasta portátil para este PC e o PC gaming. **Não submeter nem publicar na Microsoft Store.** O acompanhamento `nexus-pronta-a-usar` foi removido. O Partner Center foi observado em rascunho; não foi feita uma nova submissão.

## Implementação

- Dashboard nativo WinUI com seis indicadores e nove cartões de funções. Gráficos CPU/RAM usam as amostras existentes, sem previsões.
- Disposição adapta-se à largura: indicadores em duas/três/seis colunas; funções em uma/duas/três colunas. Conteúdo excedente permanece acessível por deslocação vertical.
- Jogos e perfis lidos da biblioteca SQLite deste computador. Não se inventam CS2/FiveM instalados.
- Três preferências visuais Windows consultadas em leitura. Cada alteração é revista, guardada no diário e reposta pelo mecanismo existente; uma opção já configurada sem histórico não recebe um padrão inventado.
- Acessos reais a Mouse Lab, cinco treinos, sensibilidade, Game Boost, GPU, processos, DNS/latência, WARP, limpeza revista, Defender e restauro.
- FPS, temperaturas e latência sem medições aparecem como indisponíveis/por medir. Identificação de GPU não é medição de carga.
- O painel não instala mods/citizens, não aplica opções de driver não suportadas, não altera anti-cheat nem instala drivers.
- Tema preto/verde e apresentação clara preservada. Preferências, favoritos, perfis e tabelas existentes não são eliminados ou reiniciados.

## Dados e recuperação

Dados pessoais continuam em `%LOCALAPPDATA%/NexusOptimizer`, separados da pasta dos binários. Antes da validação foi feita uma cópia SQLite consistente, com verificação de integridade, e da aparência em `outputs/NexusBackups/private-before-1.0.1-20261020`. Essa cópia é privada e não faz parte de nenhum ZIP de distribuição. As publicações 0.10 e 1.0 foram mantidas.

Não copiar o diário de undo entre computadores: os valores anteriores pertencem ao PC onde foram capturados. No outro PC, extrair a pasta inteira e abrir `Nexus.UI.exe`; os dados começam numa pasta local própria. Não é necessário instalar o SDK .NET.

## Validação e assinatura

Compilação/publicação local 1.0.1 concluídas. O primeiro teste de interface identificou um erro ao reutilizar controlos XAML antes de a árvore estar carregada; corrigido removendo-os explicitamente do contentor original. O binário corrigido e a DLL de testes foram bloqueados pelo App Control (`0x800711C7`). Smart App Control permaneceu em estado 1; nenhuma política foi alterada. A validação da interface e regressão da revisão exata tem de ser confirmada no Windows de testes; isto não substitui o arranque nos PCs do utilizador.

A versão portátil continua sem assinatura de distribuição. Uma pasta completa resolve dependências em falta, mas não garante aceitação por Smart App Control ou políticas empresariais. Não desativar proteções, não importar um certificado de desenvolvimento como se fosse de confiança pública e não anunciar a app como pronta num PC onde ainda não abriu. A distribuição assinada direta exige um certificado/serviço legítimo que ainda não está disponível; não foi contratado um serviço pago.

Os resultados finais e os hashes ficam no relatório de entrega, fora dos dados privados.
