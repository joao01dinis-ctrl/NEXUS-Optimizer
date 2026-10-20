# Utilização — NEXUS 0.7

Revisão experimental gratuita. O bloqueio de código sem assinatura pelo Windows pode impedir abrir. Não alterar proteções para executar esta revisão. Confirmar o estado de arranque em VALIDATION.md.

## PC Boost

Escolher Balanceado (predefinido), Competitivo ou Streaming. Analisar o PC recolhe cinco amostras durante cerca de dez segundos, sem alterar definições. Rever os motivos e escolher Aplicar seleção. Só diferenças de interface justificadas por pressão sustentada ficam pré-selecionadas. Opções manuais do Competitivo ficam por selecionar. A análise expira em cinco minutos; Cancelar interrompe sem aplicar. Diagnóstico e recomendações contém atalhos para CPU/RAM, arranque, espaço, energia e gráficos. Exportar diagnóstico guarda JSON local com hardware e nomes das aplicações, sem envio. O botão Restaurar alterações repõe valores anteriores guardados; conflitos externos ficam no Histórico. Não são mostrados ganhos estimados como resultados reais.

## Tweaks e Windows

Cada cartão mostra o estado consultado no Windows. Ligar guarda o estado anterior e aplica. Desligar repõe esse estado. Já configurado sem alteração anterior da NEXUS fica só para consulta. Algumas políticas não são suportadas pela edição instalada. Teclas de Aderência afeta a acessibilidade e o atalho Shift; manter se necessário.

## Game Mode e FiveM

Abrir o jogo, Atualizar, selecionar o processo e Adicionar à biblioteca. No FiveM, selecionar o processo do jogo que efetivamente renderiza, não apenas o launcher. Balanceado mantém as definições e pode só acompanhar o jogo. Competitivo e Streaming mantêm prioridade original por defeito. Escolher o perfil, rever as opções de prioridade/animações/plano e iniciar sessão manualmente ou ativar Automático. Energia por sessão exige corrente e poupança desligada; se a alimentação mudar, a sessão com alteração de energia é terminada e reposta. Confirmar a lista apresentada. O automático só vale nesta abertura, com configuração fixa; pausar/reativar para autorizar alterações na biblioteca ou opções. Ao terminar o processo, a NEXUS tenta repor a sessão. Terminar e repor faz o mesmo manualmente. Após um encerramento inesperado, recuperar a sessão anterior antes de iniciar outra.

## App Tuner, rede e limpeza

App Tuner permite prioridade acima do normal para um programa elegível aberto. Rede compara DNS; alterações exigem administrador e revisão. Latência mede ICMP ao IP escolhido, não latência de entrada. Memória tem consulta de processos e limpeza manual revista. Auto Limpeza mostra intervalo/estado e executa apenas temporários locais antigos dentro dos limites; não esvazia a Lixeira.

## Confirmar se ajudou

Usar Testes e FPS para importar capturas antes/depois do mesmo FiveM/servidor/cenário. FPS e pausas dependem também do servidor, scripts, assets e hardware. A NEXUS não captura o jogo automaticamente nem apresenta um ganho sem medições. Ver FIVEM-VALIDATION.md.

Para comparar repetições: guardar pelo menos três capturas como Referência e três como Otimizado, com as mesmas condições descritas; abrir Comparação de testes repetidos, selecionar os grupos e Comparar repetições. Não usar o mesmo CSV ou outros fluxos dele como repetições. O resultado mostra medianas/faixas e indica quando é inconclusivo; não confirma a causa da diferença. 0,1% low só aparece com mil ou mais intervalos válidos.
