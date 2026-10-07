# Referência funcional e limites

Referências públicas consultadas em 7/10/2026: https://tloptimizer.com/ e https://tloptimizer.com/como-usar . Não foi obtido código, conteúdo pago nem instalado o produto concorrente.

O site anuncia mais de 30 ajustes. O vídeo fornecido pelo utilizador em 7/10/2026 acrescenta uma lista extensa de funções, inventariada em VIDEO-FEATURES.md. Algumas opções Premium estão desfocadas, as avançadas Game Mode não foram expandidas e alguns motores mostram erros. Os rótulos visíveis não revelam a implementação interna nem comprovam ganhos. Não é possível afirmar paridade funcional integral, nem inventar o comportamento das opções ocultas.

| Área | NEXUS 0.2 |
|---|---|
| Energia | Aplicar planos existentes; undo persistente |
| Prioridade de CPU | Aplicação escolhida, AboveNormal; identidade PID+arranque; undo |
| Efeitos visuais / teclado | Animações de conteúdo e oito preferências por API Windows; undo; repetição não mede latência |
| Restauro | Journal por alteração, recuperação de escrita interrompida; atalho para proteção do sistema |
| Modo de Jogo / GPU | Acesso às definições oficiais; não há ativação automática |
| Serviços | Não implementado; preservar compatibilidade e serviços essenciais |
| Sessões | Seleção de processo aberto, energia/prioridade opcionais, histórico, reposição ao terminar enquanto o NEXUS estiver aberto; recuperação manual após reiniciar |
| Memória | RAM total e lista de processos por RAM residente/privada; sem esvaziar working set, standby ou páginas modificadas |
| Rede | Comparação de quatro DNS com amostras válidas, timeout e cancelamento; não aplica DNS nem renova IP |
| Limpeza | Seleção de temporários do utilizador com 7 dias sem escrita/acesso, revalidação e reciclagem; não limpa shaders, Prefetch, launchers nem esvazia Lixeira |
| FPS / latência de entrada | Não medidos; nenhum ganho artificial apresentado |
| Arranque, discos, drivers, GPU, Modo de Jogo, gravação, transparência, notificações, privacidade e apps | Atalhos identificados para ferramentas Windows; não são motores próprios equivalentes ao TL |
| Conta, assinatura comercial | Não necessárias no NEXUS |

A implementação é própria e o visual original. O utilizador escolhe as alterações e confirma-as dentro da app. Todas as funções implementadas estão disponíveis gratuitamente.

## Opções que exigem tratamento diferente

Desativar SmartScreen, atualizações automáticas ou Recuperação do Sistema conflita com o pedido original de preservar a segurança e evitar tweaks agressivos. Não foram implementados botões para essas ações. Desativar Modern Standby, paginação, economia USB/PCIe/rede, serviços Xbox/Search/SysMain e remover componentes em massa pode causar perda de funcionalidades; não faz parte da 0.2. Hibernação, relógio UTC e caminhos longos são opções de armazenamento/compatibilidade, não ganhos universais de FPS. Estes limites são explícitos, sem apresentar atalhos ou funções ausentes como equivalentes automáticos.

O catálogo de privacidade por aplicação, telemetria de Office/Firefox/Chrome/NVIDIA/Visual Studio, modificações de serviços/políticas, desinstalação/reinstalação em massa, auto limpeza periódica, motor adaptativo, biblioteca automática de jogos e métricas de FPS/latência de entrada continuam por implementar. As opções ocultas precisam de identificação antes de poderem ser comparadas. A versão atual é uma etapa funcional própria, não o catálogo completo solicitado.

## Referências técnicas

Preferências: [SystemParametersInfoW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow). Reciclagem: [IFileOperation::SetOperationFlags](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags) e [PostDeleteItem](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperationprogresssink-postdeleteitem). Biblioteca SQLite corrigida: [CVE-2025-6965](https://github.com/advisories/GHSA-2m69-gcr7-jv3q).
