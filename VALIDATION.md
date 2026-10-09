# Validação 0.6 — 09/10/2026

- Solução Release x64: **0 erros e 0 avisos**. Publicação completa auto contida em `artifacts/app-v0.6-final`, sem assinatura de distribuição.
- Suite final: **116 aprovados, 0 falhados, 2 ignorados, total 118**. Os ignorados são integrações opt-in de reciclagem e preferência Windows. A reciclagem foi validada na 0.2; não foi repetida porque não mudou.
- **Integração real de preferência Windows aprovada separadamente:** animação dos menus original `1`, aplicada `0`, reposta `1`; journal `reverted`. Dados isolados em `artifacts/native-preference-validation/20261009-185638` e resultado em `artifacts/tests/native-preference-v0.6.trx`. Não se alterou outra preferência, segurança ou dados de jogos.
- Testes novos: restauro de cadeias/valores não padrão, conflitos externos preservados, separação de ownership, estados obsoletos, falhas e escritas interrompidas no PC Boost com recuperação, biblioteca automática imutável/revogação, falha sem repetição, reutilização de PID e exclusão de processos protegidos. São testes de lógica com executores simulados; não comprovam o efeito em jogos.
- Uma execução intermédia foi bloqueada ao carregar a DLL de testes. A execução final passou após corrigir um aviso do teste, sem alterar proteções ou certificados. O script de build e CI recusam agora zero testes executados como validação.
- **Arranque final bloqueado:** `Nexus.Restore.dll`, controlo de aplicações Windows `0x800711C7`, saída 1. Não foi criada imagem da interface 0.6 nem validada a navegação/renderização neste PC. A tentativa inicial desta revisão teve o mesmo bloqueio.
- UI redesenhada no código: navegação por áreas, 14 cartões de preferências, políticas/Teclas de Aderência diretas, PC Boost em conjunto, modo de jogo automático, App Tuner, restauro global e Auto Limpeza própria. XAML compilado; apresentação e interações ainda precisam de validação de arranque.
- CI preparado para executar testes, round-trip nativo num runner descartável e arranque/captura de três páginas, sem aplicar ajustes durante o smoke test. Só a execução remota concluída confirma esse resultado; não valida políticas/arranque no PC do utilizador.
- Nenhuma sessão real FiveM ou ganho de FPS/latência foi medido. DNS, políticas, working sets, remoção de pacotes, automatismo real de jogo e execução sob identidade MSIX continuam por validar. Não há paridade integral comprovada com TL Optimizer.
- SignPath: candidatura enviada em 6/10 e recusada em 7/10 por falta de adoção pública suficiente. Microsoft Store permanece em preparação; não existe certificação ou MSIX assinado.

Evidência: `artifacts/tests/build-v0.6-final.log`, `tests-v0.6-final.trx`, `native-preference-v0.6.trx`, `artifacts/native-preference-validation/*/result.json` e `artifacts/startup-v0.6-final-2026-10-09/startup-error.txt`. A revisão não deve ser anunciada como pronta a usar neste PC.

---
# Validação 0.5 — 09/10/2026

- Release da solução: **0 erros e 0 avisos**; .NET 10.0.401 / Windows x64.
- Testes finais: **99 aprovados, 0 falhados, 1 ignorado, total 100**. A integração Shell opt-in de reciclagem continua ignorada; os restantes testes usam definições simuladas e dados próprios, salvo a leitura passiva de processos.
- Novas verificações cobrem intervalos FPS, pausas preservadas, separação de aplicações/PID/fluxos, CSV inválido, limites e cancelamento, origem com SHA-256, comparação de condições/duração, histórico SQLite e backup. CPU por aplicação: normalização por tempo/CPUs lógicas, rejeição de PID reutilizado/counters inválidos, leitura Windows sem alterações e cancelamento.
- Uma execução anterior teve 35 testes impedidos de carregar bibliotecas por Controlo de Aplicações (0x800711C7). A execução final passou sem alterar proteções, certificados, allowlists ou políticas do Windows. Esse resultado não é garantia para futuras revisões/outros PCs.
- Publicação completa auto contida em `artifacts/app-v0.5-final`, com recursos WinUI, todos os módulos e sem assinatura de distribuição.
- **O arranque isolado final foi confirmado**: saída 0, `dashboard.png` com o guia FiveM e `smoke-test.json` com CPU/GPU/RAM/disco. O arranque anterior da 0.5 também mostrou a nova página de capturas FPS. Não foram aplicados ajustes nestes testes.
- Corrigida a navegação para iniciar cada página no topo; detalhes do método/origem de FPS recolhidos num painel expansível e comparação antiga limpa ao mudar a seleção.
- Verificação na interface final: botão de CPU executado com amostras reais; seletor de ficheiros abriu e importou um CSV artificial identificado como TEST-FIXTURE-NOT-A-GAME; cálculo apresentado, análise guardada e confirmada em SQLite. O comparador recusou usar a mesma captura como antes/depois. Dados e evidência ficam em `artifacts/ui-v0.5-final-2026-10-09`; não entram na distribuição. Journal de ajustes e sessões vazios durante este teste.
- As descrições de 18 publicações TikTok foram inventariadas. O player dos vídeos testados falhou; não se afirma ter visto os vídeos nem reproduzido todos os ajustes do TL. Ver `TIKTOK-REVIEW.md`.
- Guia FiveM com diagnóstico oficial e protocolo de comparação: **nenhuma sessão real de FiveM nem ganho no jogo foi medido**. CSVs artificiais de testes não são resultados de jogo.
- Efeitos reais de DNS/políticas/remoção de pacotes, working sets e perfis em jogos continuam por validar. Compilação, testes de lógica e arranque não comprovam eficácia de todos os executores.
- Catálogo: 138 entradas da referência mais duas ferramentas próprias de verificação, não 140 executores. A revisão não tem paridade integral com TL Optimizer.
- Estado GitHub antigo da PR 1 corresponde à 0.2. Esta evidência é local; não é uma release pública assinada ou uma aprovação da Store.

Evidência: `artifacts/tests/build-v0.5-final.log`, `artifacts/tests/tests-v0.5-final.trx`, `artifacts/startup-v0.5-final-2026-10-09/launcher-result.txt`, `dashboard.png` e `smoke-test.json`. O MSIX da Store exige validação própria sob identidade de pacote.

---
# Validação 0.4 — 08/10/2026

- SDK .NET 10.0.401 e Windows x64; dependências restauradas a partir da configuração da solução.
- Release da solução: **0 erros e 0 avisos**. O compilador WinUI aceitou as páginas, nomes e handlers.
- Testes Release: **78 aprovados, 0 falhados, 1 ignorado, total 79**. O teste ignorado é a integração Shell opt-in, que já foi validada na 0.2; não foi repetida com ficheiros do utilizador.
- As verificações novas cobrem captura/restauro de DNS estático e automático com executor simulado, rejeição de alvos/valores inválidos sem escrita, políticas com ausência/external changes, recuperação das animações de sessão, biblioteca persistente e rejeição de comandos/UNC, cálculo ICMP/cancelamento antes de enviar, proteção de processo antes de abrir, classificação do catálogo, reaplicação que guarda o valor externo e limites de idade/quantidade/tamanho da auto limpeza.
- Publicação completa: `artifacts/app-v0.4`, auto contida x64 com recursos WinUI e todos os módulos. Continua **NotSigned**.
- Tentativa de arranque isolada, sem otimizações: o Windows recusou iniciar o próprio `Nexus.UI.exe`, com a mensagem **Uma política de Controlo de Aplicações bloqueou este ficheiro**. A app não criou uma sessão/processo utilizável; não existem imagem ou métricas de arranque validadas da 0.4. O diagnóstico fica em `artifacts/startup-v0.4-2026-10-08/launcher-error.txt`.
- **Nenhum DNS, política, working set, concessão IP ou pacote instalado foi alterado/removido no PC para validar esta revisão.** Os testes criam apenas dados/ficheiros descartáveis próprios. Não se alterou segurança, certificados ou regras de Controlo de Aplicações.
- Ainda não foram validados efeitos reais de DNS/políticas/remoção de pacote, deteção numa sessão real de jogo, working sets, temporizadores e apresentação visual das novas páginas. Compilar e passar testes de lógica não confirma esses efeitos nem prontidão neste PC.
- A revisão é local; os resultados antigos do GitHub/PR 1 são da 0.2 e não validam a 0.4. Não foi criada uma release pública da 0.4.
- O catálogo tem **138 entradas rastreáveis**, incluindo grupos compostos e desconhecidos. Não equivale a 138 executores. A cobertura exata, exclusões e pendências está em `TL-PARITY.md` e na página Catálogo. A 0.4 não tem paridade integral com TL Optimizer.

Evidência: `artifacts/tests/tests-v0.4-release.trx`, `artifacts/startup-v0.4-2026-10-08/launcher-error.txt`, pacote completo e código/documentação desta revisão. Uma assinatura confiável continua pendente e o arranque terá de ser confirmado após qualquer solução de distribuição; não existe garantia de que apenas assinar resolve todas as políticas de um PC.

---
# Validação 0.3 — 08/10/2026

## Resultado atual

- Restore da solução atualizado, sem avisos de vulnerabilidade.
- Solução Release x64: 0 erros e 0 avisos.
- Conjunto de testes Release: 64 aprovados, 0 falhados e 1 integração opt-in ignorada (65 no total). O teste Windows Shell de reciclagem foi validado separadamente na 0.2; não foi repetido porque essa implementação não mudou.
- Novos testes verificam cancelamento sem resultado parcial, medidas finitas, rejeição de duração excessiva/resultados inválidos, comparação por ambiente/método, persistência do benchmark e backup independente que preserva valores/ownership e não sobrescreve dados.
- Publicação self-contained completa em artifacts/app-v0.3 com recursos WinUI e runtimes.
- Arranque final recusado ao carregar Nexus.Restore.dll: política de Controlo de Aplicações, 0x800711C7. O tratamento de falha registou startup-error.txt e terminou com código 1, sem ficar em execução. Não existe captura nem validação visual da interface 0.3.
- Nenhum ajuste real de energia, prioridade, interface, rede ou limpeza foi aplicado nesta validação. Não foram alteradas proteções, políticas ou certificados do Windows.

Evidência local: artifacts/tests/tests-v0.3-release.trx e artifacts/startup-v0.3-final-2026-10-08/startup-error.txt. Os testes usam dados isolados e definições simuladas; a curta medição de CPU/cópia de memória num teste não altera definições.

A 0.3 não deve ser anunciada como pronta a usar neste PC. Não tem paridade integral com TL Optimizer. O benchmark não mede FPS nem prova ganhos. A exportação não importa nem repõe o histórico automaticamente.

O resultado GitHub previamente confirmado corresponde à 0.2 na PR1, commit a2bcc880: build e 57 testes passaram. A 0.3 foi validada localmente; esse resultado remoto antigo não valida as novas funções.

## Histórico anterior

# Validação 0.2 — 07/10/2026

## Resultado atual

Solução final Release x64 compilada com 0 avisos e 0 erros. Publicação self-contained gerada em artifacts/app-v0.2, incluindo recursos WinUI. Microsoft.Data.Sqlite 10.0.12 e SQLite nativo 3.50.3; restore final sem o aviso de vulnerabilidade observado na versão antiga.

Validações isoladas, com definições simuladas salvo a reciclagem de um ficheiro descartável de teste:

- 24 testes de diário/sessões passaram: persistência, recuperação, migração, concorrência, ownership, reposição seletiva, abertura de duas instâncias e rejeição de prioridades não reversíveis (configuração Debug).
- 9 testes de análise/reciclagem passaram, incluindo alterações de candidatos, limites, cancelamento e junctions (Release x64).
- Teste de integração Windows Shell passou: só um ficheiro criado em artifacts/recycle-validation foi enviado à Lixeira, confirmado pelo callback com destino. A Lixeira não foi esvaziada e os temporários reais do utilizador não foram limpos. Esse teste é opt-in através de NEXUS_TEST_RECYCLE_INTEGRATION=1.
- 13 testes DNS passaram em Debug, incluindo respostas inválidas, mediana, timeouts e cancelamento.

O conjunto completo final Debug passou: 57 aprovados, 0 falhados e 1 ignorado (integração de reciclagem opt-in, validada separadamente). Resultado em artifacts/tests/tests-v0.2-debug-final.trx. No conjunto final Release, 44 passaram e 13 testes de rede foram impedidos de executar por Controlo de Aplicações ao carregar Nexus.Network.dll (0x800711C7); 1 integração opt-in ignorada. Resultado em artifacts/tests/tests-v0.2-release-final.trx. Este bloqueio não é apresentado como aprovação dos testes Release de rede.

O arranque de artifacts/app-v0.2/Nexus.UI.exe foi recusado pelo Windows antes de criar a janela. Não há captura nem validação visual da interface 0.2. A versão não deve ser anunciada como pronta a usar neste PC. A 0.1 abriu anteriormente em 7/10, mas esse resultado não prova que a 0.2 abre.

Eventos Windows CodeIntegrity 3077 confirmam bloqueios de ficheiros recompilados. Nenhuma proteção, política de assinatura ou serviço de segurança foi alterado. Não foram aplicados ajustes novos de energia/prioridade/interface aos valores reais do PC durante esta validação.

O workflow GitHub está preparado para compilar e executar todos os testes num runner Windows e publicar artefactos sem assinatura. O resultado remoto da 0.2 só pode ser confirmado após envio do código e conclusão da execução. Esse teste não substitui arranque no PC do utilizador nem assinatura de distribuição.

## Histórico da 0.1 — 29/09/2026

## Concluído

- SDK Microsoft .NET 10.0.401 local, arquivo oficial validado por SHA-512.
- Solução com os 12 projetos solicitados, compilada em Release x64.
- Build: 0 erros e 0 avisos.
- 11 testes xUnit aprovados, 0 falhados e 0 ignorados.
- Testes cobrem perfil inicial, persistência, mudanças sem efeito, rejeição de perfis inválidos, undo em ordem inversa, undo após reinício, nova alteração após undo, intervalos conservadores, RAM real, identificação de hardware e CPU após duas amostras.
- Publicação self-contained em `artifacts/app`, incluindo .NET, Windows App SDK, SQLite nativo, XBF e PRI.
- Serilog registou corretamente a falha de arranque.
- Repositório Git local e instruções de compilação incluídos.

## Bloqueio de execução

O teste do executável publicado encontrou primeiro recursos XAML em falta; a publicação foi corrigida para incluir `App.xbf`, `MainWindow.xbf` e `Nexus.UI.pri`.

Na tentativa seguinte, a criação da janela avançou até à construção do view model, onde o Windows recusou carregar `Nexus.Core.dll`:

```
System.IO.FileLoadException
Uma política de Controlo de Aplicações bloqueou este ficheiro. (0x800711C7)
```

Esse bloqueio impede confirmar o dashboard completo, interações dos botões e captura visual no ambiente atual. Não foram desativados Smart App Control, Defender, firewall, políticas de execução ou outros mecanismos de segurança. É necessária uma via de distribuição/assinatura aceite pela política do dispositivo para completar essa validação.

Os testes automatizados de Core, Hardware, Storage, Optimization e Restore executaram com sucesso antes desta tentativa. Isto não substitui a verificação visual da interface.

## Evidência local

- `artifacts/tests/tests.trx`: resultados dos testes.
- `artifacts/validation/build.log`: build final.
- `artifacts/validation/publish.log`: publicação.
- `artifacts/validation/startup.log`: diagnóstico do arranque bloqueado.

As pastas de artefactos não são incluídas no histórico Git. A solução, o código, os testes e os scripts são versionados.
