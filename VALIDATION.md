# Validação 0.2 — 07/10/2026

## Resultado atual

Solução final Release x64 compilada com 0 avisos e 0 erros. Publicação self-contained gerada em artifacts/app-v0.2, incluindo recursos WinUI. Microsoft.Data.Sqlite 10.0.12 e SQLite nativo 3.50.3; restore final sem o aviso de vulnerabilidade observado na versão antiga.

Validações isoladas, com definições simuladas salvo a reciclagem de um ficheiro descartável de teste:

- 19 testes de diário/sessões passaram: persistência, recuperação, migração, concorrência, ownership e reposição seletiva (configuração Debug).
- 9 testes de análise/reciclagem passaram, incluindo alterações de candidatos, limites, cancelamento e junctions (Release x64).
- Teste de integração Windows Shell passou: só um ficheiro criado em artifacts/recycle-validation foi enviado à Lixeira, confirmado pelo callback com destino. A Lixeira não foi esvaziada e os temporários reais do utilizador não foram limpos. Esse teste é opt-in através de NEXUS_TEST_RECYCLE_INTEGRATION=1.
- 13 testes DNS escritos e compilados; a execução neste PC foi bloqueada antes das verificações ao carregar Nexus.Network.dll. Não são apresentados como aprovados.

O teste completo da compilação final Release foi bloqueado ao carregar Nexus.Tests.dll por política de Controlo de Aplicações (0x800711C7). Uma execução anterior, com artefacto parcial, registou 11 pass e 24 falhas de carregamento por essa mesma política; esse artefacto não incluía todos os novos testes e não valida a solução final.

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
