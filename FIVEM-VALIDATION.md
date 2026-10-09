# Validar a NEXUS no FiveM

A página Jogos tem um guia de diagnóstico e atalhos para as medições. Não aplica
um suposto perfil FiveM nem altera os ficheiros do jogo, recursos ou anti-cheat.
Ainda não existe um resultado de desempenho deste PC numa sessão real de FiveM.

No FiveM, F8 abre a consola. `cl_drawperf true` mostra métricas incluindo FPS,
ping e perda de pacotes; `cl_drawperf false` oculta-as. Estes comandos de utilizador
estão na [documentação oficial Cfx](https://docs.fivem.net/docs/client-manual/console-commands/).
A NEXUS não os executa e não ativa o modo de programador do FiveM.

1. Regista resolução, qualidade gráfica, versão do jogo, driver, limite de FPS,
   VSync e o local/percurso no servidor escolhido. Mantém o portátil ligado à
   mesma alimentação e deixa terminar os carregamentos iniciais.
2. Faz três capturas PresentMon do mesmo percurso durante 30–60 segundos cada.
   Escolhe o processo que apresenta o jogo, não o launcher/overlay. Guarda os CSVs
   com métricas v1 e importa-os em Testar desempenho. Vê [FPS-VALIDATION.md](FPS-VALIDATION.md).
3. Consulta CPU por aplicação durante uma sessão equivalente. A medição é uma
   amostra curta e não identifica, sozinha, a causa das pausas.
4. Revê apenas um ajuste reversível de cada vez, conservando o valor anterior.
   Repete as três capturas com as mesmas condições. Não limpes caches entre testes.
5. Compara FPS, 1% low e P99 entre repetições. Se a carga/cena/servidor mudou ou a
   diferença não se repetir, não atribuas a variação ao ajuste.

FPS, pausas e ligação têm medições distintas. A comparação DNS da NEXUS não mede
a ligação ao servidor FiveM. ICMP a outro IP também não é o ping do jogo.
Não foram aplicados ajustes reais nem realizada uma sessão de jogo para escrever
este protocolo. As capturas artificiais dos testes não são resultados FiveM.
